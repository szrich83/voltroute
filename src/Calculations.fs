namespace VoltRoute

open WebSharper

[<JavaScript>]
module Calculations =

    type TripInput =
        {
            BatteryCapacity : float
            ConsumptionPer100Km : float
            DistanceKm : float
            ElectricityPrice : float
            StateOfChargePercent : float
            ChargingPowerKw : float
            TargetSocPercent : float
            AverageChargerIntervalKm : float
        }

    type ChargingStop =
        {
            StopNumber : int
            ArrivalSocPercent : float
            TargetSocPercent : float
            ChargedEnergyKWh : float
            ChargeTimeHours : float
            ChargeTimeMinutes : int
            DriveDistanceKm : float
        }

    type TripResult =
        {
            AvailableEnergyKWh : float
            AvailableRangeKm : float
            EnergyNeededKWh : float
            TripCost : float
            NeedsCharging : bool
            ChargingNeededKWh : float
            ChargingTimeHours : float
            RemainingEnergyKWh : float
            RemainingSocPercent : float
            ChargingStops : int
            ChargingStopDetails : ChargingStop list
        }

    let clamp minValue maxValue value =
        value |> max minValue |> min maxValue

    let calculateAvailableEnergy batteryCapacity socPercent =
        batteryCapacity * (socPercent / 100.0)

    let calculateSocFromEnergy batteryCapacity energy =
        if batteryCapacity <= 0.0 then 0.0
        else (energy / batteryCapacity) * 100.0

    let calculateAvailableRange availableEnergy consumptionPer100Km =
        if consumptionPer100Km <= 0.0 then 0.0
        else (availableEnergy / consumptionPer100Km) * 100.0

    let calculateEnergyNeeded distanceKm consumptionPer100Km =
        (distanceKm / 100.0) * consumptionPer100Km

    let calculateTripCost energyNeeded electricityPrice =
        energyNeeded * electricityPrice

    let calculateChargingNeeded energyNeeded availableEnergy =
        max 0.0 (energyNeeded - availableEnergy)

    let calculateRemainingEnergy availableEnergy energyNeeded =
        max 0.0 (availableEnergy - energyNeeded)

    let calculateRemainingSoc remainingEnergy batteryCapacity =
        if batteryCapacity <= 0.0 then 0.0
        else (remainingEnergy / batteryCapacity) * 100.0

    let chargingPowerFactor soc =
        if soc < 20.0 then 0.85
        elif soc < 60.0 then 1.0
        elif soc < 80.0 then 0.65
        else 0.30

    let estimateChargingTimeSegment batteryCapacity chargingPowerKw fromSoc toSoc =
        if batteryCapacity <= 0.0 || chargingPowerKw <= 0.0 || toSoc <= fromSoc then
            0.0
        else
            let step = 5.0

            let rec loop currentSoc acc =
                if currentSoc >= toSoc then
                    acc
                else
                    let nextSoc = min toSoc (currentSoc + step)
                    let socDelta = nextSoc - currentSoc
                    let energyToAdd = batteryCapacity * (socDelta / 100.0)
                    let effectivePower = chargingPowerKw * chargingPowerFactor currentSoc

                    let hours =
                        if effectivePower <= 0.0 then 0.0
                        else energyToAdd / effectivePower

                    loop nextSoc (acc + hours)

            loop fromSoc 0.0

    let chooseDynamicTargetSoc userTargetSoc totalDistance remainingAfterDrive =
        let distanceRatio =
            if totalDistance <= 0.0 then 0.0
            else remainingAfterDrive / totalDistance

        let heuristicTargetSoc =
            if distanceRatio > 0.60 then 80.0
            elif distanceRatio > 0.30 then 70.0
            else 60.0

        min userTargetSoc heuristicTargetSoc

    /// Returns the latest reachable charger position assuming chargers are available
    /// every `chargerSpacingKm`. The charger spacing is availability, not a mandatory stop distance.
    let chooseChargerStopDistance maxLegDistance chargerSpacingKm remainingDistance =
        let spacing =
            chargerSpacingKm
            |> max 20.0

        let latestUsefulDistance =
            min maxLegDistance remainingDistance

        let reachableChargerIndex =
            floor (latestUsefulDistance / spacing)

        if reachableChargerIndex < 1.0 then
            latestUsefulDistance
        else
            let chargerDistance = reachableChargerIndex * spacing

            // If chargerDistance is too tiny due to some odd edge case, just use max safe range.
            if chargerDistance < 1.0 then
                latestUsefulDistance
            else
                chargerDistance

    let simulateChargingStops (input: TripInput) =
        let userTargetSoc = clamp 20.0 95.0 input.TargetSocPercent

        let startEnergy =
            calculateAvailableEnergy input.BatteryCapacity input.StateOfChargePercent

        let reserveSoc = 10.0
        let reserveEnergy =
            calculateAvailableEnergy input.BatteryCapacity reserveSoc

        let rec loop stopNumber currentEnergy remainingDistance acc =
            if remainingDistance <= 0.0 then
                List.rev acc
            else
                let usableEnergyThisLeg =
                    max 0.0 (currentEnergy - reserveEnergy)

                let maxLegDistance =
                    calculateAvailableRange usableEnergyThisLeg input.ConsumptionPer100Km

                if remainingDistance <= maxLegDistance then
                    List.rev acc
                else
                    let driveDistance =
                        chooseChargerStopDistance
                            maxLegDistance
                            input.AverageChargerIntervalKm
                            remainingDistance

                    let energyUsedThisLeg =
                        calculateEnergyNeeded driveDistance input.ConsumptionPer100Km

                    let arrivalEnergy =
                        max 0.0 (currentEnergy - energyUsedThisLeg)

                    let arrivalSoc =
                        calculateSocFromEnergy input.BatteryCapacity arrivalEnergy

                    let remainingAfterDrive =
                        remainingDistance - driveDistance

                    let energyNeededForRemaining =
                        calculateEnergyNeeded remainingAfterDrive input.ConsumptionPer100Km

                    let dynamicTargetSoc =
                        chooseDynamicTargetSoc userTargetSoc input.DistanceKm remainingAfterDrive

                    let fullTargetEnergy =
                        calculateAvailableEnergy input.BatteryCapacity dynamicTargetSoc

                    let energyNeededToFinishWithReserve =
                        energyNeededForRemaining + reserveEnergy

                    let nextTargetEnergy =
                        min fullTargetEnergy energyNeededToFinishWithReserve

                    let nextTargetSoc =
                        calculateSocFromEnergy input.BatteryCapacity nextTargetEnergy
                        |> clamp arrivalSoc 95.0

                    let chargedEnergy =
                        max 0.0 (nextTargetEnergy - arrivalEnergy)

                    let chargeTime =
                        estimateChargingTimeSegment
                            input.BatteryCapacity
                            input.ChargingPowerKw
                            arrivalSoc
                            nextTargetSoc

                    if chargedEnergy < 0.01 || nextTargetSoc <= arrivalSoc + 0.1 then
                        List.rev acc
                    else
                        let stop =
                            {
                                StopNumber = stopNumber
                                ArrivalSocPercent = arrivalSoc
                                TargetSocPercent = nextTargetSoc
                                ChargedEnergyKWh = chargedEnergy
                                ChargeTimeHours = chargeTime
                                ChargeTimeMinutes = int (System.Math.Round(chargeTime * 60.0))
                                DriveDistanceKm = driveDistance
                            }

                        loop (stopNumber + 1) nextTargetEnergy remainingAfterDrive (stop :: acc)

        loop 1 startEnergy input.DistanceKm []

    let calculateTrip (input: TripInput) =
        let effectiveTargetSoc = clamp 20.0 95.0 input.TargetSocPercent

        let availableEnergy =
            calculateAvailableEnergy input.BatteryCapacity input.StateOfChargePercent

        let availableRange =
            calculateAvailableRange availableEnergy input.ConsumptionPer100Km

        let energyNeeded =
            calculateEnergyNeeded input.DistanceKm input.ConsumptionPer100Km

        let tripCost =
            calculateTripCost energyNeeded input.ElectricityPrice

        let chargingNeeded =
            calculateChargingNeeded energyNeeded availableEnergy

        let remainingEnergy =
            calculateRemainingEnergy availableEnergy energyNeeded

        let remainingSoc =
            calculateRemainingSoc remainingEnergy input.BatteryCapacity

        let stopDetails =
            if energyNeeded <= availableEnergy then
                []
            else
                simulateChargingStops {
                    input with
                        TargetSocPercent = effectiveTargetSoc
                        AverageChargerIntervalKm = max 20.0 input.AverageChargerIntervalKm
                }

        let chargingStops =
            List.length stopDetails

        let chargingTime =
            stopDetails
            |> List.sumBy (fun s -> s.ChargeTimeHours)

        {
            AvailableEnergyKWh = availableEnergy
            AvailableRangeKm = availableRange
            EnergyNeededKWh = energyNeeded
            TripCost = tripCost
            NeedsCharging = energyNeeded > availableEnergy
            ChargingNeededKWh = chargingNeeded
            ChargingTimeHours = chargingTime
            RemainingEnergyKWh = remainingEnergy
            RemainingSocPercent = remainingSoc
            ChargingStops = chargingStops
            ChargingStopDetails = stopDetails
        }
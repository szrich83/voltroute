namespace VoltRoute

open WebSharper

[<JavaScript>]
module Calculations =

    /// Input parameters required for EV trip simulation.
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

    /// Represents one charging stop during the trip.
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

    /// Final aggregated trip result shown in the UI.
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

    /// Restricts a value to a safe numeric interval.
    let clamp minValue maxValue value =
        value |> max minValue |> min maxValue

    /// Converts battery capacity and SOC into currently available energy.
    let calculateAvailableEnergy batteryCapacity socPercent =
        batteryCapacity * (socPercent / 100.0)

    /// Converts absolute battery energy back to SOC percentage.
    let calculateSocFromEnergy batteryCapacity energy =
        if batteryCapacity <= 0.0 then 0.0
        else (energy / batteryCapacity) * 100.0

    /// Estimates theoretical range from available energy and consumption.
    let calculateAvailableRange availableEnergy consumptionPer100Km =
        if consumptionPer100Km <= 0.0 then 0.0
        else (availableEnergy / consumptionPer100Km) * 100.0

    /// Calculates how much energy is needed for a given trip distance.
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

    /// Simplified SOC-dependent charging curve.
    /// Charging is fastest in the mid-range and slows down at high SOC.
    let chargingPowerFactor soc =
        if soc < 20.0 then 0.85
        elif soc < 60.0 then 1.0
        elif soc < 80.0 then 0.65
        else 0.30

    /// Estimates charging time between two SOC values using a simplified step-based simulation.
    /// The charging power is reduced depending on the current SOC,
    /// which produces more realistic results than a linear charge model.
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

    /// Chooses a dynamic target SOC for the next charge stop.
    /// The idea is to avoid charging too high unnecessarily:
    /// - early in a long trip, higher targets can reduce future risk
    /// - later in the trip, lower targets are often faster and more efficient
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
    ///
    /// This means the vehicle does not stop at every charger,
    /// only at the furthest charger that is still safely reachable.
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

            // Fallback protection for edge cases where the calculated charger distance
            // would become unrealistically small.
            if chargerDistance < 1.0 then
                latestUsefulDistance
            else
                chargerDistance

    /// Simulates the trip step-by-step and generates all required charging stops.
    ///
    /// Main ideas:
    /// - a reserve SOC is kept for safety
    /// - the car drives to the latest safely reachable charger
    /// - the next charging target is chosen dynamically
    /// - charging stops are skipped if no meaningful charging is needed
    let simulateChargingStops (input: TripInput) =
        let userTargetSoc = clamp 20.0 95.0 input.TargetSocPercent

        let startEnergy =
            calculateAvailableEnergy input.BatteryCapacity input.StateOfChargePercent

        // Safety reserve to avoid planning trips that arrive at 0% SOC.
        let reserveSoc = 10.0
        let reserveEnergy =
            calculateAvailableEnergy input.BatteryCapacity reserveSoc

        let rec loop stopNumber currentEnergy remainingDistance acc =
            if remainingDistance <= 0.0 then
                List.rev acc
            else
                // Only the energy above the reserve is considered usable for the next leg.
                let usableEnergyThisLeg =
                    max 0.0 (currentEnergy - reserveEnergy)

                let maxLegDistance =
                    calculateAvailableRange usableEnergyThisLeg input.ConsumptionPer100Km

                // If the remaining distance is reachable without another stop,
                // the simulation ends.
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

                    // The next charge should ideally be just enough to continue efficiently,
                    // while still preserving the safety reserve.
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

                    // Avoid creating meaningless stops caused by rounding or tiny differences.
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

    /// Calculates the overall trip result used by the UI.
    /// If the trip cannot be completed on the initial battery,
    /// the charging stop simulation is executed.
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
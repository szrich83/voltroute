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
        }

    let calculateAvailableEnergy batteryCapacity socPercent =
        batteryCapacity * (socPercent / 100.0)

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

    let estimateChargingStops distanceKm startRangeKm perStopRangeKm =
        if distanceKm <= startRangeKm then
            0
        else
            let remainingAfterStart = distanceKm - startRangeKm
            int (ceil (remainingAfterStart / perStopRangeKm))

    let normalizeTargetSoc targetSoc =
        targetSoc
        |> max 20.0
        |> min 95.0

    let calculateTrip (input: TripInput) =
        let effectiveTargetSoc = normalizeTargetSoc input.TargetSocPercent

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

        let fullUsableEnergyPerStop =
            input.BatteryCapacity * ((effectiveTargetSoc - 10.0) / 100.0)

        let perStopRangeKm =
            calculateAvailableRange fullUsableEnergyPerStop input.ConsumptionPer100Km

        let chargingStops =
            estimateChargingStops input.DistanceKm availableRange perStopRangeKm

        let chargingTime =
            if energyNeeded <= availableEnergy then
                0.0
            else
                let perStopChargeTime =
                    estimateChargingTimeSegment
                        input.BatteryCapacity
                        input.ChargingPowerKw
                        10.0
                        effectiveTargetSoc

                float chargingStops * perStopChargeTime

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
        }
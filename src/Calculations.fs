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

    let calculateChargingTime chargingNeeded chargingPowerKw =
        if chargingPowerKw <= 0.0 then 0.0
        else chargingNeeded / chargingPowerKw

    let calculateRemainingEnergy availableEnergy energyNeeded =
        max 0.0 (availableEnergy - energyNeeded)

    let calculateRemainingSoc remainingEnergy batteryCapacity =
        if batteryCapacity <= 0.0 then 0.0
        else (remainingEnergy / batteryCapacity) * 100.0

    let calculateTrip (input: TripInput) =
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

        let chargingTime =
            calculateChargingTime chargingNeeded input.ChargingPowerKw

        let remainingEnergy =
            calculateRemainingEnergy availableEnergy energyNeeded

        let remainingSoc =
            calculateRemainingSoc remainingEnergy input.BatteryCapacity

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
        }
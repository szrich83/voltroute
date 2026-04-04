namespace VoltRoute

open WebSharper

[<JavaScript>]
module VehiclePresets =

    /// Represents a predefined EV configuration that can populate
    /// the input form with realistic vehicle-specific values.
    type VehiclePreset =
        {
            Id : string
            Brand : string
            Model : string
            BatteryCapacity : float
            ConsumptionPer100Km : float
            ChargingPowerKw : float
        }

    /// Predefined EV presets used for quick vehicle selection in the UI.
    /// These values provide realistic defaults for battery size,
    /// average consumption, and charging capability.
    let presets =
        [
            {
                Id = "tesla-model-3-sr"
                Brand = "Tesla"
                Model = "Model 3 Standard Range"
                BatteryCapacity = 60.0
                ConsumptionPer100Km = 14.0
                ChargingPowerKw = 170.0
            }
            {
                Id = "tesla-model-3-lr"
                Brand = "Tesla"
                Model = "Model 3 Long Range"
                BatteryCapacity = 75.0
                ConsumptionPer100Km = 16.0
                ChargingPowerKw = 170.0
            }
            {
                Id = "tesla-model-3-performance"
                Brand = "Tesla"
                Model = "Model 3 Performance"
                BatteryCapacity = 75.0
                ConsumptionPer100Km = 17.0
                ChargingPowerKw = 170.0
            }
            {
                Id = "tesla-model-y-rwd"
                Brand = "Tesla"
                Model = "Model Y RWD"
                BatteryCapacity = 60.0
                ConsumptionPer100Km = 15.5
                ChargingPowerKw = 170.0
            }
            {
                Id = "tesla-model-y-lr"
                Brand = "Tesla"
                Model = "Model Y Long Range"
                BatteryCapacity = 75.0
                ConsumptionPer100Km = 17.0
                ChargingPowerKw = 170.0
            }
            {
                Id = "hyundai-kona-39"
                Brand = "Hyundai"
                Model = "Kona Electric 39 kWh"
                BatteryCapacity = 39.0
                ConsumptionPer100Km = 14.8
                ChargingPowerKw = 50.0
            }
            {
                Id = "hyundai-kona-64"
                Brand = "Hyundai"
                Model = "Kona Electric 64 kWh"
                BatteryCapacity = 64.0
                ConsumptionPer100Km = 15.5
                ChargingPowerKw = 77.0
            }
            {
                Id = "kia-niro-ev"
                Brand = "Kia"
                Model = "Niro EV 64.8 kWh"
                BatteryCapacity = 64.8
                ConsumptionPer100Km = 16.2
                ChargingPowerKw = 80.0
            }
            {
                Id = "nissan-leaf-40"
                Brand = "Nissan"
                Model = "Leaf 40 kWh"
                BatteryCapacity = 40.0
                ConsumptionPer100Km = 17.0
                ChargingPowerKw = 50.0
            }
            {
                Id = "nissan-leaf-62"
                Brand = "Nissan"
                Model = "Leaf e+ 62 kWh"
                BatteryCapacity = 62.0
                ConsumptionPer100Km = 18.0
                ChargingPowerKw = 70.0
            }
            {
                Id = "bmw-i3-42"
                Brand = "BMW"
                Model = "i3 42.2 kWh"
                BatteryCapacity = 42.2
                ConsumptionPer100Km = 14.5
                ChargingPowerKw = 50.0
            }
            {
                Id = "vw-id3-pro"
                Brand = "Volkswagen"
                Model = "ID.3 Pro"
                BatteryCapacity = 58.0
                ConsumptionPer100Km = 15.8
                ChargingPowerKw = 120.0
            }
            {
                Id = "vw-id4-pro"
                Brand = "Volkswagen"
                Model = "ID.4 Pro"
                BatteryCapacity = 77.0
                ConsumptionPer100Km = 17.5
                ChargingPowerKw = 135.0
            }
            {
                Id = "skoda-enyaq-60"
                Brand = "Skoda"
                Model = "Enyaq 60"
                BatteryCapacity = 62.0
                ConsumptionPer100Km = 16.8
                ChargingPowerKw = 120.0
            }
            {
                Id = "skoda-enyaq-80"
                Brand = "Skoda"
                Model = "Enyaq 80"
                BatteryCapacity = 82.0
                ConsumptionPer100Km = 17.8
                ChargingPowerKw = 135.0
            }
            {
                Id = "renault-zoe-ze50"
                Brand = "Renault"
                Model = "Zoe ZE50"
                BatteryCapacity = 52.0
                ConsumptionPer100Km = 16.0
                ChargingPowerKw = 50.0
            }
        ]

    /// Returns the distinct list of available brands for the first dropdown.
    let brands =
        presets
        |> List.map (fun p -> p.Brand)
        |> List.distinct
        |> List.sort

    /// Returns all vehicle presets that belong to the selected brand.
    let modelsForBrand brand =
        presets
        |> List.filter (fun p -> p.Brand = brand)

    /// Finds a preset by its unique identifier.
    let tryFindPresetById presetId =
        presets |> List.tryFind (fun p -> p.Id = presetId)
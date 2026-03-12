namespace VoltRoute

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open VoltRoute.Calculations

[<JavaScript>]
module Client =

    let battery = Var.Create "75"
    let consumption = Var.Create "18"
    let distance = Var.Create "240"
    let price = Var.Create "0.25"
    let soc = Var.Create "80"
    let chargingPower = Var.Create "50"

    let availableEnergyText = Var.Create "-"
    let availableRangeText = Var.Create "-"
    let energyNeededText = Var.Create "-"
    let tripCostText = Var.Create "-"
    let needsChargingText = Var.Create "-"
    let chargingNeededText = Var.Create "-"
    let chargingTimeText = Var.Create "-"
    let remainingEnergyText = Var.Create "-"
    let remainingSocText = Var.Create "-"
    let errorText = Var.Create ""

    let format2 (value: float) =
        sprintf "%.2f" value

    let calculate () =
        try
            errorText.Value <- ""

            let input : TripInput =
                {
                    BatteryCapacity = float battery.Value
                    ConsumptionPer100Km = float consumption.Value
                    DistanceKm = float distance.Value
                    ElectricityPrice = float price.Value
                    StateOfChargePercent = float soc.Value
                    ChargingPowerKw = float chargingPower.Value
                }

            let result = calculateTrip input

            availableEnergyText.Value <- sprintf "%.2f kWh" result.AvailableEnergyKWh
            availableRangeText.Value <- sprintf "%.2f km" result.AvailableRangeKm
            energyNeededText.Value <- sprintf "%.2f kWh" result.EnergyNeededKWh
            tripCostText.Value <- sprintf "%.2f €" result.TripCost
            needsChargingText.Value <- if result.NeedsCharging then "Yes" else "No"
            chargingNeededText.Value <- sprintf "%.2f kWh" result.ChargingNeededKWh
            chargingTimeText.Value <- sprintf "%.2f h" result.ChargingTimeHours
            remainingEnergyText.Value <- sprintf "%.2f kWh" result.RemainingEnergyKWh
            remainingSocText.Value <- sprintf "%.2f %%" result.RemainingSocPercent
        with
        | _ ->
            errorText.Value <- "Invalid input. Please enter valid numeric values."

    let field labelText valueVar =
        div [ attr.``class`` "field" ] [
            label [ attr.``class`` "label" ] [ text labelText ]
            Doc.InputType.Text [ attr.``class`` "input" ] valueVar
        ]

    let resultRow labelText valueView =
        div [ attr.``class`` "result-row" ] [
            span [ attr.``class`` "result-label" ] [ text labelText ]
            span [ attr.``class`` "result-value" ] [ textView valueView ]
        ]

    [<SPAEntryPoint>]
    let Main () =
        calculate ()

        div [ attr.``class`` "page" ] [

            div [ attr.``class`` "card" ] [
                h1 [ attr.``class`` "title" ] [ text "VoltRoute – EV Trip Planner" ]

                p [ attr.``class`` "subtitle" ] [
                    text "Estimate EV energy use, range, charging need, and trip cost."
                ]

                div [ attr.``class`` "grid" ] [
                    field "Battery capacity (kWh)" battery
                    field "Consumption (kWh / 100 km)" consumption
                    field "Trip distance (km)" distance
                    field "Electricity price (€/kWh)" price
                    field "State of charge (%)" soc
                    field "Charging power (kW)" chargingPower
                ]

                div [ attr.``class`` "button-row" ] [
                    button [
                        attr.``class`` "calculate-button"
                        on.click (fun _ _ -> calculate())
                    ] [
                        text "Calculate"
                    ]
                ]

                div [ attr.``class`` "error-text" ] [
                    textView errorText.View
                ]
            ]

            div [ attr.``class`` "card results-card" ] [
                h2 [ attr.``class`` "results-title" ] [ text "Results" ]

                resultRow "Available energy" availableEnergyText.View
                resultRow "Available range" availableRangeText.View
                resultRow "Energy needed" energyNeededText.View
                resultRow "Trip cost" tripCostText.View
                resultRow "Needs charging" needsChargingText.View
                resultRow "Charging needed" chargingNeededText.View
                resultRow "Charging time" chargingTimeText.View
                resultRow "Remaining energy" remainingEnergyText.View
                resultRow "Remaining SOC" remainingSocText.View
            ]
        ]
        |> Doc.RunById "main"
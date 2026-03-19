namespace VoltRoute

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html
open VoltRoute.Calculations
open VoltRoute.VehiclePresets

[<JavaScript>]
module Client =

    let selectedBrand = Var.Create ""
    let selectedModelId = Var.Create ""

    let battery = Var.Create "75"
    let consumption = Var.Create "18"
    let distance = Var.Create "240"
    let price = Var.Create "0.25"
    let soc = Var.Create "80"
    let chargingPower = Var.Create "50"
    let targetSoc = Var.Create "80"

    let availableEnergyText = Var.Create "-"
    let availableRangeText = Var.Create "-"
    let energyNeededText = Var.Create "-"
    let tripCostText = Var.Create "-"
    let needsChargingText = Var.Create "-"
    let chargingNeededText = Var.Create "-"
    let chargingTimeText = Var.Create "-"
    let remainingEnergyText = Var.Create "-"
    let remainingSocText = Var.Create "-"
    let chargingStopsText = Var.Create "-"
    let errorText = Var.Create ""

    let applyPreset presetId =
        match tryFindPresetById presetId with
        | Some preset ->
            battery.Value <- string preset.BatteryCapacity
            consumption.Value <- string preset.ConsumptionPer100Km
            chargingPower.Value <- string preset.ChargingPowerKw
        | None ->
            ()

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
                    TargetSocPercent = float targetSoc.Value
                }

            let result = calculateTrip input

            availableEnergyText.Value <- sprintf "%.2f kWh" result.AvailableEnergyKWh
            availableRangeText.Value <- sprintf "%.2f km" result.AvailableRangeKm
            energyNeededText.Value <- sprintf "%.2f kWh" result.EnergyNeededKWh
            tripCostText.Value <- sprintf "%.2f €" result.TripCost
            needsChargingText.Value <- if result.NeedsCharging then "Yes" else "No"
            chargingNeededText.Value <- sprintf "%.2f kWh" result.ChargingNeededKWh
            chargingTimeText.Value <- sprintf "%.2f h" result.ChargingTimeHours
            chargingStopsText.Value <- string result.ChargingStops

            if result.NeedsCharging then
                remainingEnergyText.Value <- "N/A"
                remainingSocText.Value <- "N/A"
            else
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

    let statusRow labelText valueView =
        div [ attr.``class`` "result-row" ] [
            span [ attr.``class`` "result-label" ] [ text labelText ]

            span [ attr.``class`` "result-value" ] [
                Doc.BindView (fun v ->
                    let isYes = (v = "Yes")

                    span [
                        attr.``class`` (
                            "status-badge " +
                            (if isYes then "status-yes" else "status-no")
                        )
                    ] [
                        text v
                    ]
                ) valueView
            ]
        ]

    let chargingStopsHighlight valueView =
        div [ attr.``class`` "highlight-box" ] [
            div [ attr.``class`` "highlight-left" ] [
                span [ attr.``class`` "highlight-icon" ] [ text "⚡" ]
                span [ attr.``class`` "highlight-label" ] [ text "Estimated charging stops" ]
            ]

            span [ attr.``class`` "highlight-value" ] [
                textView valueView
            ]
        ]

    let chargingTimeHighlight valueView =
        div [ attr.``class`` "highlight-box highlight-time" ] [
            div [ attr.``class`` "highlight-left" ] [
                span [ attr.``class`` "highlight-icon" ] [ text "⏱" ]
                span [ attr.``class`` "highlight-label" ] [ text "Estimated charging time" ]
            ]

            span [ attr.``class`` "highlight-value" ] [
                textView valueView
            ]
        ]

    let brandField () =
        div [ attr.``class`` "field" ] [
            label [ attr.``class`` "label" ] [ text "Brand" ]

            select [
                attr.``class`` "input"
                on.change (fun el _ ->
                    let value = string el?value
                    selectedBrand.Value <- value
                    selectedModelId.Value <- ""
                )
            ] [
                option [ attr.value "" ] [ text "Select brand" ]
                yield!
                    brands
                    |> List.map (fun brand ->
                        option [ attr.value brand ] [ text brand ]
                    )
            ]
        ]

    let modelField () =
        let modelOptionsDoc =
            selectedBrand.View
            |> View.Map (fun brand ->
                let baseOptions =
                    [ option [ attr.value "" ] [ text "Select model" ] ]

                let options =
                    if brand = "" then
                        baseOptions
                    else
                        baseOptions @
                        (
                            modelsForBrand brand
                            |> List.map (fun preset ->
                                option [ attr.value preset.Id ] [ text preset.Model ]
                            )
                        )

                Doc.Concat options
            )

        div [ attr.``class`` "field" ] [
            label [ attr.``class`` "label" ] [ text "Model" ]

            select [
                attr.``class`` "input"
                on.change (fun el _ ->
                    let value = string el?value
                    selectedModelId.Value <- value
                    applyPreset value
                    calculate ()
                )
            ] [
                Doc.BindView id modelOptionsDoc
            ]
        ]

    [<SPAEntryPoint>]
    let Main () =
        calculate ()

        div [ attr.``class`` "page" ] [

            div [ attr.``class`` "card" ] [
                div [ attr.``class`` "hero" ] [
                    h1 [ attr.``class`` "title" ] [ text "VoltRoute – EV Trip Planner" ]

                    p [ attr.``class`` "subtitle" ] [
                    text "Estimate EV energy use, range, charging strategy, charging stops, and trip cost."
                    ]
                ]

                div [ attr.``class`` "grid" ] [
                    brandField ()
                    modelField ()
                    field "Battery capacity (kWh)" battery
                    field "Consumption (kWh / 100 km)" consumption
                    field "Trip distance (km)" distance
                    field "Electricity price (€/kWh)" price
                    field "State of charge (%)" soc
                    field "Charging power (kW)" chargingPower
                    field "Target charge (%)" targetSoc
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

                div [ attr.``class`` "highlight-stack" ] [
                    chargingStopsHighlight chargingStopsText.View
                    chargingTimeHighlight chargingTimeText.View
                ]

                resultRow "Available energy" availableEnergyText.View
                resultRow "Available range" availableRangeText.View
                resultRow "Energy needed" energyNeededText.View
                resultRow "Trip cost" tripCostText.View
                statusRow "Needs charging" needsChargingText.View
                resultRow "Charging needed" chargingNeededText.View
                resultRow "Remaining energy" remainingEnergyText.View
                resultRow "Remaining SOC" remainingSocText.View
            ]
        ]
        |> Doc.RunById "main"
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

    /// UI state for preset selection.
    let selectedBrand = Var.Create ""
    let selectedModelId = Var.Create ""

    /// Input fields bound to the form.
    let battery = Var.Create "75"
    let consumption = Var.Create "18"
    let distance = Var.Create "240"
    let price = Var.Create "0.25"
    let soc = Var.Create "80"
    let chargingPower = Var.Create "50"
    let targetSoc = Var.Create "80"
    let chargerInterval = Var.Create "120"

    /// Output fields displayed in the results panel.
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
    let chargingStopDetails = Var.Create<List<ChargingStop>>([])
    let socChartPoints = Var.Create<List<float * float>>([])

    /// Applies the selected vehicle preset to the main input fields.
    /// This allows quick comparison between predefined EV models.
    let applyPreset presetId =
        match tryFindPresetById presetId with
        | Some preset ->
            battery.Value <- string preset.BatteryCapacity
            consumption.Value <- string preset.ConsumptionPer100Km
            chargingPower.Value <- string preset.ChargingPowerKw
        | None ->
            ()

    /// Builds chart points for SOC visualization across the full trip.
    ///
    /// The chart contains:
    /// - the starting SOC at distance 0
    /// - arrival SOC at each charging stop
    /// - post-charge SOC at the same stop distance
    /// - final SOC at total trip distance
    ///
    /// Repeating the same x-coordinate for arrival and target SOC creates
    /// the visible vertical jump that represents charging on the graph.
    let buildSocChartPoints (input: TripInput) (result: TripResult) =
        let startSoc = input.StateOfChargePercent
        let totalDistance = input.DistanceKm
        let batteryCapacity = input.BatteryCapacity
        let consumptionPer100Km = input.ConsumptionPer100Km

        if List.isEmpty result.ChargingStopDetails then
            [
                (0.0, startSoc)
                (totalDistance, result.RemainingSocPercent)
            ]
        else
            let mutable currentDistance = 0.0
            let mutable points : List<float * float> = [ (0.0, startSoc) ]

            for stop in result.ChargingStopDetails do
                currentDistance <- currentDistance + stop.DriveDistanceKm
                points <- points @ [ (currentDistance, stop.ArrivalSocPercent) ]
                points <- points @ [ (currentDistance, stop.TargetSocPercent) ]

            let drivenUntilLastCharge =
                result.ChargingStopDetails
                |> List.sumBy (fun s -> s.DriveDistanceKm)

            let remainingFinalDistance =
                max 0.0 (totalDistance - drivenUntilLastCharge)

            let finalSoc =
                match List.tryLast result.ChargingStopDetails with
                | None ->
                    result.RemainingSocPercent
                | Some lastStop ->
                    let startFinalEnergy =
                        batteryCapacity * (lastStop.TargetSocPercent / 100.0)

                    let finalLegEnergyNeeded =
                        (remainingFinalDistance / 100.0) * consumptionPer100Km

                    let finalEnergy =
                        max 0.0 (startFinalEnergy - finalLegEnergyNeeded)

                    if batteryCapacity <= 0.0 then 0.0
                    else (finalEnergy / batteryCapacity) * 100.0

            points @ [ (totalDistance, finalSoc) ]

    /// Reads the current form values, executes the trip calculation,
    /// and updates every UI output field.
    ///
    /// If parsing fails, the UI is reset to a safe state and an error is shown.
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
                    AverageChargerIntervalKm = float chargerInterval.Value
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
            chargingStopDetails.Value <- result.ChargingStopDetails
            socChartPoints.Value <- buildSocChartPoints input result

            if result.NeedsCharging then
                remainingEnergyText.Value <- "N/A"
                remainingSocText.Value <- "N/A"
            else
                remainingEnergyText.Value <- sprintf "%.2f kWh" result.RemainingEnergyKWh
                remainingSocText.Value <- sprintf "%.2f %%" result.RemainingSocPercent
        with
        | _ ->
            errorText.Value <- "Invalid input. Please enter valid numeric values."
            chargingStopDetails.Value <- []
            socChartPoints.Value <- []

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

    /// Special result row for boolean charging status.
    /// A colored badge is used instead of plain text for better visual emphasis.
    let statusRow labelText valueView =
        div [ attr.``class`` "result-row" ] [
            span [ attr.``class`` "result-label" ] [ text labelText ]
            span [ attr.``class`` "result-value" ] [
                Doc.BindView
                    (fun v ->
                        let isYes = (v = "Yes")

                        span [
                            attr.``class`` (
                                "status-badge " +
                                (if isYes then "status-yes" else "status-no")
                            )
                        ] [
                            text v
                        ])
                    valueView
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

    let timelineSeparator =
        div [ attr.``class`` "timeline-separator" ] []

    let startNode =
        div [ attr.``class`` "timeline-node timeline-start" ] [
            div [ attr.``class`` "timeline-icon" ] [ text "◎" ]
            div [ attr.``class`` "timeline-label" ] [ text "Start" ]
        ]

    let endNode =
        div [ attr.``class`` "timeline-node timeline-end" ] [
            div [ attr.``class`` "timeline-icon" ] [ text "⚑" ]
            div [ attr.``class`` "timeline-label" ] [ text "End" ]
        ]

    /// Renders one charging stop node in the trip timeline.
    let chargeNode (stop: ChargingStop) =
        div [ attr.``class`` "timeline-node timeline-charge" ] [
            div [ attr.``class`` "timeline-icon" ] [ text "↯" ]
            div [ attr.``class`` "timeline-label" ] [ text (sprintf "Charge %d" stop.StopNumber) ]
            div [ attr.``class`` "timeline-stop-meta" ] [
                div [] [ text (sprintf "~%d min" stop.ChargeTimeMinutes) ]
                div [] [ text (sprintf "%.0f km" stop.DriveDistanceKm) ]
                div [] [
                    text (sprintf "%.0f%% → %.0f%%" stop.ArrivalSocPercent stop.TargetSocPercent)
                ]
            ]
        ]

    /// Builds a visual trip timeline from charging stop details.
    /// The sequence is rendered as:
    /// start -> charge 1 -> charge 2 -> ... -> end
    let chargingTimeline detailsView =
        div [ attr.``class`` "timeline-card" ] [
            h3 [ attr.``class`` "timeline-title" ] [ text "Trip timeline" ]

            Doc.BindView
                (fun details ->
                    let rec build docs remainingStops =
                        match remainingStops with
                        | [] ->
                            docs @ [ timelineSeparator; endNode ]
                        | stop :: tail ->
                            build (docs @ [ timelineSeparator; chargeNode stop ]) tail

                    let docs =
                        build [ startNode ] details

                    div [ attr.``class`` "timeline-row" ] docs)
                detailsView

            div [ attr.``class`` "timeline-hint" ] [
                text "Stops are chosen from reachable chargers based on the configured charger spacing."
            ]
        ]

    /// Renders a simple custom SOC chart without using an external chart library.
    ///
    /// The chart uses absolute-positioned HTML elements:
    /// - horizontal grid lines for SOC reference levels
    /// - rotated divs as line segments
    /// - point markers for important state changes
    let socChart pointsView =
        let chartWidth = 860.0
        let chartHeight = 320.0
        let padLeft = 48.0
        let padRight = 16.0
        let padTop = 16.0
        let padBottom = 34.0
        let plotWidth = chartWidth - padLeft - padRight
        let plotHeight = chartHeight - padTop - padBottom

        let gridSocValues = [ 0.0; 25.0; 50.0; 75.0; 100.0 ]

        div [ attr.``class`` "card soc-card" ] [
            h2 [ attr.``class`` "results-title" ] [ text "SOC graph" ]

            Doc.BindView
                (fun points ->
                    if List.isEmpty points then
                        div [] [ text "No chart data available." ]
                    else
                        let maxDistance =
                            points
                            |> List.map fst
                            |> List.max
                            |> max 1.0

                        let scaleX distanceKm =
                            padLeft + (distanceKm / maxDistance) * plotWidth

                        let scaleY socPercent =
                            padTop + ((100.0 - socPercent) / 100.0) * plotHeight

                        let lineDocs =
                            points
                            |> List.pairwise
                            |> List.map (fun ((x1, y1), (x2, y2)) ->
                                let px1 = scaleX x1
                                let py1 = scaleY y1
                                let px2 = scaleX x2
                                let py2 = scaleY y2

                                let dx = px2 - px1
                                let dy = py2 - py1
                                let length = sqrt (dx * dx + dy * dy)
                                let angle = atan2 dy dx * 180.0 / System.Math.PI

                                div [
                                    attr.``class`` "soc-line"
                                    attr.style (
                                        sprintf
                                            "left: %.2fpx; top: %.2fpx; width: %.2fpx; transform: rotate(%.2fdeg);"
                                            px1 py1 length angle
                                    )
                                ] []
                            )

                        let pointDocs =
                            points
                            |> List.map (fun (distanceKm, socPercent) ->
                                let px = scaleX distanceKm
                                let py = scaleY socPercent

                                div [
                                    attr.``class`` "soc-point"
                                    attr.style (
                                        sprintf
                                            "left: %.2fpx; top: %.2fpx;"
                                            px py
                                    )
                                ] []
                            )

                        let yGridDocs =
                            gridSocValues
                            |> List.collect (fun socValue ->
                                let py = scaleY socValue

                                [
                                    div [
                                        attr.``class`` "soc-grid-line"
                                        attr.style (sprintf "top: %.2fpx;" py)
                                    ] []

                                    div [
                                        attr.``class`` "soc-grid-label"
                                        attr.style (sprintf "top: %.2fpx;" py)
                                    ] [
                                        text (sprintf "%.0f%%" socValue)
                                    ]
                                ])

                        let xLabelDocs =
                            [ 0.0; maxDistance / 2.0; maxDistance ]
                            |> List.map (fun km ->
                                let px = scaleX km
                                div [
                                    attr.``class`` "soc-x-label"
                                    attr.style (sprintf "left: %.2fpx;" px)
                                ] [
                                    text (sprintf "%.0f km" km)
                                ])

                        div [] [
                            div [ attr.``class`` "soc-chart-shell" ] [
                                div [ attr.``class`` "soc-plot" ] (
                                    yGridDocs @ lineDocs @ pointDocs @ xLabelDocs
                                )
                            ]

                            div [ attr.``class`` "soc-chart-legend" ] [
                                text "Driving lowers SOC; reachable chargers based on charger spacing create the charging jumps."
                            ]
                        ])
                pointsView
        ]

    /// Brand selector.
    /// Changing the brand resets the currently selected model,
    /// because model options depend on the chosen brand.
    let brandField () =
        div [ attr.``class`` "field" ] [
            label [ attr.``class`` "label" ] [ text "Brand" ]

            select [
                attr.``class`` "input"
                on.change (fun el _ ->
                    let value = string el?value
                    selectedBrand.Value <- value
                    selectedModelId.Value <- "")
            ] [
                option [ attr.value "" ] [ text "Select brand" ]
                yield!
                    brands
                    |> List.map (fun brand ->
                        option [ attr.value brand ] [ text brand ])
            ]
        ]

    /// Model selector.
    /// The available models are filtered dynamically by the selected brand.
    /// Choosing a model automatically applies its preset and recalculates the trip.
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
                                option [ attr.value preset.Id ] [ text preset.Model ])
                        )

                Doc.Concat options)

        div [ attr.``class`` "field" ] [
            label [ attr.``class`` "label" ] [ text "Model" ]

            select [
                attr.``class`` "input"
                on.change (fun el _ ->
                    let value = string el?value
                    selectedModelId.Value <- value
                    applyPreset value
                    calculate ())
            ] [
                Doc.BindView id modelOptionsDoc
            ]
        ]

    [<SPAEntryPoint>]
    let Main () =
        // Run one initial calculation so the page shows valid default results immediately.
        calculate ()

        div [ attr.``class`` "page" ] [

            div [ attr.``class`` "hero" ] [
                h1 [ attr.``class`` "title" ] [ text "VoltRoute – EV Trip Planner" ]
                p [ attr.``class`` "subtitle" ] [
                    text "Estimate EV energy use, range, charging strategy, charging stops, and trip cost."
                ]
            ]

            div [ attr.``class`` "card" ] [
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
                    field "Charger spacing (km)" chargerInterval
                ]

                div [ attr.``class`` "button-row" ] [
                    button [
                        attr.``class`` "calculate-button"
                        on.click (fun _ _ -> calculate ())
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

                chargingTimeline chargingStopDetails.View

                resultRow "Available energy" availableEnergyText.View
                resultRow "Available range" availableRangeText.View
                resultRow "Energy needed" energyNeededText.View
                resultRow "Trip cost" tripCostText.View
                statusRow "Needs charging" needsChargingText.View
                resultRow "Charging needed" chargingNeededText.View
                resultRow "Remaining energy" remainingEnergyText.View
                resultRow "Remaining SOC" remainingSocText.View
            ]

            socChart socChartPoints.View
        ]
        |> Doc.RunById "main"
namespace VoltRoute

open WebSharper
open WebSharper.JavaScript
open WebSharper.UI
open WebSharper.UI.Client
open WebSharper.UI.Html

[<JavaScript>]
module Client =

    let battery = Var.Create ""
    let consumption = Var.Create ""
    let distance = Var.Create ""
    let price = Var.Create ""

    let result = Var.Create ""

    let calculate () =
        try
            let b = float battery.Value
            let c = float consumption.Value
            let d = float distance.Value
            let p = float price.Value

            let energyNeeded = (d / 100.0) * c
            let cost = energyNeeded * p

            result.Value <-
                "Energy needed: " + string energyNeeded + " kWh | Estimated cost: " + string cost + " €"
        with
            _ -> result.Value <- "Invalid input"

    [<SPAEntryPoint>]
    let Main () =

        div [] [

            h1 [] [text "VoltRoute – EV Trip Planner"]

            p [] [text "Battery capacity (kWh)"]
            Doc.Input [] battery

            p [] [text "Consumption (kWh / 100km)"]
            Doc.Input [] consumption

            p [] [text "Trip distance (km)"]
            Doc.Input [] distance

            p [] [text "Electricity price (€/kWh)"]
            Doc.Input [] price

            br [] []
            br [] []

            button [on.click (fun _ _ -> calculate())] [text "Calculate"]

            br [] []
            br [] []

            div [] [
                textView result.View
            ]

        ]
        |> Doc.RunById "main"
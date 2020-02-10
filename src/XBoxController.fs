namespace MF.XBoxController

open BrandonPotter

type Controller = private Controller of XBox.XBoxController

[<RequireQualifiedAccess>]
type ThumbPadPosition = {
    X: float
    Y: float
}

type TriggerPressed =
    | TriggerPressedPower of float

type PositionChanged =
    | ThumbPadLeft of ThumbPadPosition
    | ThumbPadRight of ThumbPadPosition
    | Lt of TriggerPressed
    | Rt of TriggerPressed

type ButtonPressed =
    | A | B | X | Y
    | Up | Down | Left | Right
    | Back | Start
    | ThumbPadLeft | ThumbPadRight
    | Lb | Rb
    | Lt | Rt

type Sensitivity =
    | Absolute
    | High
    | Medium
    | Low
    | Custom of int

[<RequireQualifiedAccess>]
module Controller =
    open System

    type Index =
        | Index of int
        | Any

    [<AutoOpen>]
    module private Controllers =
        open System.Collections.Concurrent

        type private ConnectedControllers = ConcurrentDictionary<Index, Controller>

        let private connectedControllers = ConnectedControllers()

        let addConnected (Controller controller) =
            let index = Index controller.PlayerIndex
            let connectedController = Controller controller

            connectedControllers.AddOrUpdate(index, connectedController, (fun _ _ -> connectedController))
            |> ignore

        let removeConnected index =
            connectedControllers.TryRemove index
            |> ignore

        let tryFind = function
            | Index _ as index ->
                match connectedControllers.TryGetValue index with
                | true, controller -> Some controller
                | _ -> None
            | Any ->
                connectedControllers
                |> Seq.tryHead
                |> Option.map (fun kv -> kv.Value)

        let (|IsConnected|_|) = tryFind

        let controllers () =
            connectedControllers.Values
            |> Seq.toList

        let loadConnectedControllers () =
            XBox.XBoxController.GetConnectedControllers()
            |> Seq.iter (Controller >> addConnected)

    let refreshConnectedControllers = loadConnectedControllers
    let connectedControllers = controllers
    let tryFind index = tryFind index

    let rec getAsync index =
        async {
            match index with
            | IsConnected controller -> return controller
            | _ ->
                do! Async.Sleep 1000
                return! getAsync index
        }

    let waitFor index: Async<Controller> =
        // todo - maybe all this body should be in the async{} so the watcher will be disposed after the controller is returned
        loadConnectedControllers()

        match index with
        | IsConnected controller -> async { return controller }
        | _ ->
            use watcher = new XBox.XBoxControllerWatcher()

            watcher.add_ControllerConnected(fun controller ->
                let controller = Controller controller
                controller |> addConnected
            )

            watcher.add_ControllerDisconnected(fun controller ->
                Index controller.PlayerIndex |> removeConnected
            )

            getAsync index

    let private percent = function
        | underZero when underZero < 0.0 -> 0.0
        | overHundered when overHundered > 100.0 -> 100.0
        | value -> value

    let vibrateLeft (strength, time) (Controller controller) =
        async {
            strength |> percent |> controller.SetLeftMotorVibrationSpeed
            do! Async.Sleep time
            controller.SetLeftMotorVibrationSpeed 0.0
        }

    let vibrateRight (strength, time) (Controller controller) =
        async {
            strength |> percent |> controller.SetRightMotorVibrationSpeed
            do! Async.Sleep time
            controller.SetRightMotorVibrationSpeed 0.0
        }

    let vibrate (strength, time) (Controller controller) =
        let power = strength |> percent
        async {
            power |> controller.SetLeftMotorVibrationSpeed
            power |> controller.SetRightMotorVibrationSpeed

            do! Async.Sleep time

            controller.SetLeftMotorVibrationSpeed 0.0
            controller.SetRightMotorVibrationSpeed 0.0
        }

    [<RequireQualifiedAccess>]
    module private OnPositionChanged =
        [<Measure>] type private ThumbLX
        [<Measure>] type private ThumbLY
        [<Measure>] type private ThumbRX
        [<Measure>] type private ThumbRY
        [<Measure>] type private LT
        [<Measure>] type private RT

        type PositionState = private {
            ThumbPadLeftX: float<ThumbLX>
            ThumbPadLeftY: float<ThumbLY>
            ThumbPadRightX: float<ThumbRX>
            ThumbPadRightY: float<ThumbRY>
            LTPosition: float<LT>
            RTPosition: float<RT>
        }

        let initialState = {
            ThumbPadLeftX = 50.0<ThumbLX>
            ThumbPadLeftY = 50.0<ThumbLY>
            ThumbPadRightX = 50.0<ThumbRX>
            ThumbPadRightY = 50.0<ThumbRY>
            LTPosition = 0.0<LT>
            RTPosition = 0.0<RT>
        }

        let rec execute waitTime (action: PositionChanged -> unit) (Controller controller) (state: PositionState) = async {
            if controller.IsConnected then
                let currentLtPosition: float<LT> = controller.TriggerLeftPosition * 1.0<LT>
                let currentRtPosition: float<RT> = controller.TriggerRightPosition * 1.0<RT>
                let currentThumbLX: float<ThumbLX> = controller.ThumbLeftX * 1.0<ThumbLX>
                let currentThumbLY: float<ThumbLY> = controller.ThumbLeftY * 1.0<ThumbLY>
                let currentThumbRX: float<ThumbRX> = controller.ThumbRightX * 1.0<ThumbRX>
                let currentThumbRY: float<ThumbRY> = controller.ThumbRightY * 1.0<ThumbRY>

                // Triggers

                let state =
                    if currentLtPosition <> state.LTPosition then
                        TriggerPressedPower (float currentLtPosition) |> PositionChanged.Lt |> action
                        { state with LTPosition = currentLtPosition }
                    else state

                let state =
                    if currentRtPosition <> state.RTPosition then
                        TriggerPressedPower (float currentRtPosition) |> PositionChanged.Rt |> action
                        { state with RTPosition = currentRtPosition }
                    else state

                // Thumb Left

                let state =
                    if currentThumbLX <> state.ThumbPadLeftX && currentThumbLY <> state.ThumbPadLeftY then
                        PositionChanged.ThumbPadLeft {
                            X = (float) currentThumbLX
                            Y = (float) currentThumbLY
                        }
                        |> action

                        { state with ThumbPadLeftX = currentThumbLX; ThumbPadLeftY = currentThumbLY }
                    elif currentThumbLX <> state.ThumbPadLeftX then
                        PositionChanged.ThumbPadLeft {
                            X = (float) currentThumbLX
                            Y = (float) state.ThumbPadLeftY
                        }
                        |> action

                        { state with ThumbPadLeftX = currentThumbLX }
                    elif currentThumbLY <> state.ThumbPadLeftY then
                        PositionChanged.ThumbPadLeft {
                            X = (float) state.ThumbPadLeftX
                            Y = (float) currentThumbLY
                        }
                        |> action

                        { state with ThumbPadLeftY = currentThumbLY }
                    else state

                // Thumb right

                let state =
                    if currentThumbRX <> state.ThumbPadRightX && currentThumbRY <> state.ThumbPadRightY then
                        PositionChanged.ThumbPadRight {
                            X = (float) currentThumbRX
                            Y = (float) currentThumbRY
                        }
                        |> action

                        { state with ThumbPadRightX = currentThumbRX; ThumbPadRightY = currentThumbRY }
                    elif currentThumbRX <> state.ThumbPadRightX then
                        PositionChanged.ThumbPadRight {
                            X = (float) currentThumbRX
                            Y = (float) state.ThumbPadRightY
                        }
                        |> action

                        { state with ThumbPadRightX = currentThumbRX }
                    elif currentThumbRY <> state.ThumbPadRightY then
                        PositionChanged.ThumbPadRight {
                            X = (float) state.ThumbPadRightX
                            Y = (float) currentThumbRY
                        }
                        |> action

                        { state with ThumbPadRightY = currentThumbRY }
                    else state

                // Loop ...

                do! Async.Sleep waitTime
                return! state |> execute waitTime action (Controller controller)
        }

    let onPositionChangedAsync sensitivity (action: PositionChanged -> unit) controller: Async<unit> =
        let waitTime =
            match sensitivity with
            | Absolute -> 0
            | High -> 10
            | Medium -> 20
            | Low -> 30
            | Custom value -> value

        OnPositionChanged.initialState
        |> OnPositionChanged.execute waitTime action controller

    let onButtonPressedAsync sensitivity (action: ButtonPressed -> unit) (Controller controller): Async<unit> =
        let waitTime =
            match sensitivity with
            | Absolute -> 0
            | High -> 50
            | Medium -> 100
            | Low -> 150
            | Custom value -> value

        async {
            while controller.IsConnected do
                if controller.ButtonAPressed then action A
                if controller.ButtonBPressed then action B
                if controller.ButtonXPressed then action X
                if controller.ButtonYPressed then action Y

                if controller.ButtonUpPressed then action Up
                if controller.ButtonDownPressed then action Down
                if controller.ButtonLeftPressed then action Left
                if controller.ButtonRightPressed then action Right

                if controller.ThumbpadLeftPressed then action ThumbPadLeft
                if controller.ThumbpadRightPressed then action ThumbPadRight

                if controller.ButtonShoulderLeftPressed then action Lb
                if controller.ButtonShoulderRightPressed then action Rb

                if controller.TriggerLeftPressed then action Lt
                if controller.TriggerRightPressed then action Rt

                if controller.ButtonBackPressed then action Back
                if controller.ButtonStartPressed then action Start

                do! Async.Sleep waitTime
        }

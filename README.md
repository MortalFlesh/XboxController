XBox Controller (Wrapper)
=========================

[![NuGet Version and Downloads count](https://buildstats.info/nuget/MF.XBoxController)](https://www.nuget.org/packages/MF.XBoxController)
[![Build Status](https://dev.azure.com/MortalFlesh/XBoxController/_apis/build/status/MortalFlesh.XBoxController)](https://dev.azure.com/MortalFlesh/XBoxController/_build/latest?definitionId=1)
[![Build Status](https://api.travis-ci.com/MortalFlesh/XBoxController.svg?branch=master)](https://travis-ci.com/MortalFlesh/XBoxController)

> It is F# wrapper for [BrandonPotter/XBoxController](https://github.com/BrandonPotter/XBoxController)

For now, I'm only able to use it on Windows machine, since it uses `kernel32.dll` inside, which is only on Windows.

## Simple example
```fs
async {
    let! controller = Controller.waitFor Controller.Any
    printfn "Controller is connected (%A)" controller

    let! _ =
        [
            controller |> Controller.onButtonPressedAsync Medium (function
                | button -> printfn "Button pressed %A" button
            )

            controller |> Controller.onPositionChangedAsync Medium (function
                | PositionChanged.Lt (TriggerPressedPower power) -> printfn "Lt -> %A" power
                | PositionChanged.Rt (TriggerPressedPower power) -> printfn "Rt -> %A" power

                | PositionChanged.ThumbPadLeft { X = x; Y = y } -> printfn "ThumbPadLeft X: %A, Y: %A" x y
                | PositionChanged.ThumbPadRight { X = x; Y = y } -> printfn "ThumbPadRight X: %A, Y: %A" x y
            )
        ]
        |> Async.Parallel

    return ()
}
```

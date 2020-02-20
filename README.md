XBox Controller (Wrapper)
=========================

[![NuGet Version and Downloads count](https://buildstats.info/nuget/MF.XBoxController)](https://www.nuget.org/packages/MF.XBoxController)
[![Build Status](https://dev.azure.com/MortalFlesh/XBoxController/_apis/build/status/MortalFlesh.XBoxController)](https://dev.azure.com/MortalFlesh/XBoxController/_build/latest?definitionId=1)
[![Build Status](https://api.travis-ci.com/MortalFlesh/XBoxController.svg?branch=master)](https://travis-ci.com/MortalFlesh/XBoxController)

> It is F# wrapper for [BrandonPotter/XBoxController](https://github.com/BrandonPotter/XBoxController)

For now, I'm only able to use it on Windows machine, since it uses `kernel32.dll` inside, which is only on Windows.

## Simple example
```fs
let controller =
    Controller.waitFor Controller.Any
    |> Async.RunSynchronously

printfn "Controller is connected (%A)" controller

[
    controller |> Controller.onButtonPressedAsync Medium (function
        | A -> printfn "(A)"
        | B -> printfn "(B)"
        | X -> printfn "(X)"
        | Y -> printfn "(Y)"

        | Up -> printfn "Up"
        | Down -> printfn "Down"
        | Left -> printfn "Left"
        | Right -> printfn "Right"

        | ThumbPadLeft -> printfn "<ThumbPadLeft>"
        | ThumbPadRight -> printfn "<ThumbPadRight>"

        | Lb -> printfn "[Lb]"
        | Rb -> printfn "[Rb]"

        | Lt -> printfn "[Lt]"
        | Lr -> printfn "[Lr]"

        | Back -> printfn "<Back>"
        | Start -> printfn "<Start>"
    )

    controller |> Controller.onPositionChangedAsync Medium (function
        | Lt (TriggerPressedPower power) -> printfn "Lt -> %A" power
        | Rt (TriggerPressedPower power) -> printfn "Lr -> %A" power

        | ThumpPadLeft { X = x; Y = y } -> printfn "ThumbPadLeft X: %A, Y: %A" x y
        | ThumpPadRight { X = x; Y = y } -> printfn "ThumbPadRight X: %A, Y: %A" x y
    )
]
|> Async.Parallel
|> Async.RunSynchronously
|> ignore
```

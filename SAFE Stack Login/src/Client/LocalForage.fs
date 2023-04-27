module Client.LocalForage

open Fable.Core
open Fable.Core.JS

[<ImportMember("localforage")>]
let setItem (key : string) value : Promise<unit> = jsNative

[<ImportMember("localforage")>]
let getItem (key : string) : Promise<_> = jsNative
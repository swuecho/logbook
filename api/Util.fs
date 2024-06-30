module Util

let getEnvVar varName =
        match System.Environment.GetEnvironmentVariable(varName) with
        | null -> failwith (sprintf "%s environment variable not found" varName)
        | value -> value

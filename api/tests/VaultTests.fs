module VaultTests

open System
open Xunit

let envelope () =
    let box bytes = {| iv = Convert.ToBase64String(Array.zeroCreate<byte> 12); data = Convert.ToBase64String(Array.zeroCreate<byte> bytes) |}
    {| format = "logbook-vault"; version = 1; id = Guid.NewGuid().ToString()
       kdf = {| name = "PBKDF2-SHA256"; iterations = 600000; salt = Convert.ToBase64String(Array.zeroCreate<byte> 16) |}
       master = box 48; recovery = box 48; payload = box 32 |} |> Json.Convert.toJson

[<Fact>]
let ``vault validates opaque envelopes and rejects unsupported formats and costs`` () =
    let valid = envelope ()
    Assert.True(VaultService.validEnvelope valid)
    for invalid in [ null; "{}"; "null"; valid.Replace("600000", "999999999"); valid.Replace("logbook-vault", "other"); valid.Replace("\"version\":1", "\"version\":2"); valid.Replace("\"format\":", "\"plaintext\":\"oops\",\"format\":"); String('x', 1500001) ] do
        Assert.False(VaultService.validEnvelope invalid)

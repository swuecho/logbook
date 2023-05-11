module Tests

open System
open Xunit


[<Fact>]
let ``My test`` () =
    Assert.True(true)

[<Fact>]
let ``My test2`` () =
    let ins = "在数学和计算机科学之中，算法（algorithm）为任何良定义的具体计算步骤的一个序列，常用于计算、数据处理和自动推理。精确而言，算法是一个表示为有限长列表的有效方法。算法应包含清晰定义的指令用于计算函数"
    let seq = Summary.Jieba.freqs ins
    printfn "%A" seq
    Assert.True(true)
    //Assert.True(seq)

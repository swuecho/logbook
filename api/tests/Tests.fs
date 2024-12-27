module Tests

open System
open Xunit
open TipTap

[<Fact>]
let ``My test`` () = Assert.True(true)

[<Fact>]
let ``My test2`` () =
    let ins =
        "在数学和计算机科学之中，算法（algorithm）为任何良定义的具体计算步骤的一个序列，常用于计算、数据处理和自动推理。精确而言，算法是一个表示为有限长列表的有效方法。算法应包含清晰定义的指令用于计算函数"

    let seq = Jieba.freqs ins
    printfn "%A" seq
    Assert.True(true)
//Assert.True(seq)

[<Fact>]
let ``tipTapDocJsonToMarkdown test`` () =
    let json =
        """
    {
        "type": "doc",
        "content": [
            {
                "type": "paragraph",
                "content": [
                    {
                        "type": "text",
                        "text": "This is a test."
                    }
                ]
            },
            {
                "type": "todo_list",
                "content": [
                    {
                        "type": "todo_item",
                        "attrs": {
                            "done": true
                        },
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Completed task"
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "type": "todo_item",
                        "attrs": {
                            "done": false
                        },
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Incomplete task"
                                    }
                                ]
                            }
                        ]
                    }
                ]
            }
        ]
    }
    """

    let expectedMarkdown =
        "This is a test.\n- [x] Completed task\n- [ ] Incomplete task"

    let result = TipTap.tipTapDocJsonToMarkdown json
    Assert.Equal(expectedMarkdown, result)


[<Fact>]
let ``tipTapDocJsonToMarkdown test2`` () =
    let json =
        """
    {
    "content": [
        {
            "type": "heading",
            "attrs": {
                "textAlign": null,
                "indent": null,
                "lineHeight": null,
                "level": 3
            },
            "content": [
                {
                    "type": "text",
                    "marks": [
                        {
                            "type": "link",
                            "attrs": {
                                "href": "/view?date=20240830",
                                "openInNewTab": true
                            }
                        }
                    ],
                    "text": "20240830"
                }
            ]
        },
        {
            "type": "todo_list",
            "content": [
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": true,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": null,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "TODO1"
                                }
                            ]
                        }
                    ]
                },
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": true,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": null,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "TODO2"
                                }
                            ]
                        }
                    ]
                }
            ]
        },
        {
            "type": "heading",
            "attrs": {
                "textAlign": null,
                "indent": null,
                "lineHeight": null,
                "level": 3
            },
            "content": [
                {
                    "type": "text",
                    "marks": [
                        {
                            "type": "link",
                            "attrs": {
                                "href": "/view?date=20240828",
                                "openInNewTab": true
                            }
                        }
                    ],
                    "text": "20240828"
                }
            ]
        },
        {
            "type": "todo_list",
            "content": [
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": false,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": null,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "database error (return in request)"
                                }
                            ]
                        }
                    ]
                }
            ]
        },
        {
            "type": "heading",
            "attrs": {
                "textAlign": null,
                "indent": null,
                "lineHeight": null,
                "level": 3
            },
            "content": [
                {
                    "type": "text",
                    "marks": [
                        {
                            "type": "link",
                            "attrs": {
                                "href": "/view?date=20230704",
                                "openInNewTab": true
                            }
                        }
                    ],
                    "text": "20230704"
                }
            ]
        },
        {
            "type": "todo_list",
            "content": [
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": true,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": 0,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "TODO1"
                                }
                            ]
                        }
                    ]
                },
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": true,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": 0,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "TODO2"
                                }
                            ]
                        }
                    ]
                },
                {
                    "type": "todo_item",
                    "attrs": {
                        "done": true,
                        "textAlign": null,
                        "lineHeight": null
                    },
                    "content": [
                        {
                            "type": "paragraph",
                            "attrs": {
                                "textAlign": null,
                                "indent": 0,
                                "lineHeight": null
                            },
                            "content": [
                                {
                                    "type": "text",
                                    "text": "TODO3"
                                }
                            ]
                        }
                    ]
                }
            ]
        }
    ],
    "type": "doc"
}
"""

    let expectedMarkdown =
        """
### 20240830

- [x] TODO1
- [x] TODO2

### 20240828

- [ ] database error (return in request)

### 20230704

- [x] TODO1
- [x] TODO2
- [x] TODO3"""
    let result = TipTap.tipTapDocJsonToMarkdown json
    printfn "%s" result
    Assert.Equal(expectedMarkdown, result)

[<Fact>]
let ``tipTapDocJsonToMarkdown test3`` () =
    // current dir
    let dir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "data")
    let diaryJsonText = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "20241224.json"))
    printfn "%s" diaryJsonText
    let diaryContent = diaryJsonText |> System.Text.Json.JsonDocument.Parse 
    printfn "%A" diaryContent
    let json = diaryContent.RootElement.GetProperty("note").ToString()
    printfn "%s" json

    let expectedMarkdown =
        """```
psql postgresql://hwu:using555@vultr.meihao.us:5432/hwu
```
```
curl 192.168.202.211:3001/api/random-words
```


opam cheat sheet


opam switch list-available
opam switch create 5.2.1 5.2.1
opam switch 5.2.1
opam 
eval $(opam env --switch=5.2.1)


https://stackoverflow.com/questions/40898292/how-to-install-a-specific-version-of-ocaml-compiler-with-opam"""

    let result = tipTapDocJsonToMarkdown json
    printfn "%s" result
    Assert.Equal(expectedMarkdown, result)

module Tests

open Xunit

[<Fact>]
let ``My test`` () = Assert.True(true)

[<Fact>]
let ``generatePasswordHashWithSalt preserves existing pbkdf2 format`` () =
    let password = "correct horse battery staple"
    let salt = [| for i in 0uy .. 15uy -> i |]

    let hash = Auth.generatePasswordHashWithSalt password salt

    Assert.Equal(
        "pbkdf2_sha256$260000$AAECAwQFBgcICQoLDA0ODw==$lW4qFPSClIiYgdSo62pmJTEK1ypptA2r/mZO2xIB8ZM=",
        hash
    )

[<Fact>]
let ``validatePassword accepts hashes created by generatePasswordHash`` () =
    let password = "p@ssw0rd"
    let hash = Auth.generatePasswordHash password

    Assert.True(Auth.validatePassword password hash)
    Assert.False(Auth.validatePassword "wrong-password" hash)

[<Fact>]
let ``My test2`` () =
    let ins =
        "在数学和计算机科学之中，算法（algorithm）为任何良定义的具体计算步骤的一个序列，常用于计算、数据处理和自动推理。精确而言，算法是一个表示为有限长列表的有效方法。算法应包含清晰定义的指令用于计算函数"

    let seq = TextAnalysis.freqs ins
    Assert.True(true)
//Assert.True(seq)

[<Fact>]
let ``searchTerms tokenizes mixed Chinese and English text`` () =
    let terms = TextAnalysis.searchTerms "Vue 中文 搜索"
    Assert.Contains("vue", terms)
    Assert.Contains("中文", terms)
    Assert.Contains("搜索", terms)

[<Fact>]
let ``searchTerms supports Chinese word queries`` () =
    let terms = TextAnalysis.searchTerms "今天心情很好 机器学习"
    Assert.True(terms |> Array.exists (fun term -> term.Contains("今天") || term.Contains("心情")))
    Assert.True(terms |> Array.exists (fun term -> term.Contains("机器") || term.Contains("学习")))

[<Fact>]
let ``searchIndexOfNote extracts text from tiptap json`` () =
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
                        "text": "Vue 中文 搜索"
                    }
                ]
            }
        ]
    }
    """

    let searchText, terms = TextAnalysis.searchIndexOfNote json
    Assert.Contains("Vue", searchText)
    Assert.Contains("vue", terms)

[<Fact>]
let ``compactText normalizes whitespace for snippets`` () =
    let text = " first\r\n\r\nsecond\t third "

    let result = DiaryService.compactText text

    Assert.Equal("first second third", result)

[<Fact>]
let ``buildSnippet centers long snippets on first matching term`` () =
    let text = String.replicate 90 "a " + "needle " + String.replicate 120 "b "

    let snippet = DiaryService.buildSnippet [| "needle" |] text

    Assert.Contains("needle", snippet)
    Assert.StartsWith("...", snippet)
    Assert.True(snippet.Length <= 186)

[<Fact>]
let ``summary stale queries use diary logical key`` () =
    Assert.Contains("d.note_id = s.note_id", Diary.getStaleIdsOfUserId)
    Assert.Contains("d.note_id = s.note_id", Diary.checkIdStale)
    Assert.Contains("d.user_id = @user_id", Diary.checkIdStale)
    Assert.Contains("d.note_id = @note_id", Diary.checkIdStale)
    Assert.DoesNotContain("d.id = s.id", Diary.getStaleIdsOfUserId)
    Assert.DoesNotContain("d.id = s.id", Diary.checkIdStale)

[<Fact>]
let ``todo endpoint query only loads candidate todo notes`` () =
    Assert.Contains("note LIKE '%todo_list%'", Diary.listDiaryWithTodo)
    Assert.Contains("note LIKE '%todo_list%'", Diary.listDiaryWithTodoByUserID)
    Assert.Contains("note LIKE '%todo_item%'", Diary.listDiaryWithTodoByUserID)
    Assert.Contains("note LIKE '%taskList%'", Diary.listDiaryWithTodoByUserID)
    Assert.Contains("note LIKE '%taskItem%'", Diary.listDiaryWithTodoByUserID)

[<Fact>]
let ``todo repository stores precomputed todos by note key`` () =
    Assert.Contains("INSERT INTO todo", Todo.insertOrUpdateTodo)
    Assert.Contains("ON CONFLICT (note_id, user_id)", Todo.insertOrUpdateTodo)
    Assert.Contains("DELETE FROM todo", Todo.deleteTodo)
    Assert.Contains("SELECT note_id, todos FROM todo", Todo.getTodoByUserId)

[<Fact>]
let ``extractTodoList skips notes without todo node markers`` () =
    let plainNote =
        """
        {
            "type": "doc",
            "content": [
                {
                    "type": "paragraph",
                    "content": [
                        {
                            "type": "text",
                            "text": "No tasks here"
                        }
                    ]
                }
            ]
        }
        """

    Assert.False(TipTap.containsTodoNodeMarker plainNote)
    Assert.Empty(TipTap.extractTodoList plainNote)

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
let ``tipTapDocJsonToMarkdown supports tiptap v2 task nodes`` () =
    let json =
        """
    {
        "type": "doc",
        "content": [
            {
                "type": "taskList",
                "content": [
                    {
                        "type": "taskItem",
                        "attrs": {
                            "checked": true
                        },
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Completed v2 task"
                                    }
                                ]
                            }
                        ]
                    },
                    {
                        "type": "taskItem",
                        "attrs": {
                            "checked": false
                        },
                        "content": [
                            {
                                "type": "paragraph",
                                "content": [
                                    {
                                        "type": "text",
                                        "text": "Open v2 task"
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
        "- [x] Completed v2 task\n- [ ] Open v2 task"

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
    Assert.Equal(expectedMarkdown, result)

[<Fact>]
let ``tipTapDocJsonToMarkdown test3`` () =
    // current dir
    let dir = System.IO.Path.Combine(__SOURCE_DIRECTORY__, "data")
    let diaryJsonText = System.IO.File.ReadAllText(System.IO.Path.Combine(dir, "20241224.json"))
    let diaryContent = diaryJsonText |> System.Text.Json.JsonDocument.Parse 
    let json = diaryContent.RootElement.GetProperty("note").ToString()

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

    let result = TipTap.tipTapDocJsonToMarkdown json
    Assert.Equal(expectedMarkdown, result)

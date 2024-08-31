module TipTap

open System.Text.Json

// find all todo_list in note and return todo_list as json str
let extractTodoList (note: string) =

    let rec extractTodoItems (element: JsonElement) =
        seq {
            match element.ValueKind with
            | JsonValueKind.Object ->
                match element.TryGetProperty("type") with
                | true, typeProperty when typeProperty.GetString() = "todo_list" -> yield element
                | _ ->
                    for property in element.EnumerateObject() do
                        yield! extractTodoItems property.Value
            | JsonValueKind.Array ->
                for item in element.EnumerateArray() do
                    yield! extractTodoItems item
            | _ -> ()
        }

    try
        let jsonDocument = JsonDocument.Parse(note)
        let root = jsonDocument.RootElement
        extractTodoItems root |> Seq.toList
    with ex ->
        printfn "%A" ex
        []

// Function to construct a tiptap doc with todoLists and note_id
let constructTipTapDoc
    (todoLists:
        {| noteId: string
           todoList: JsonElement list |} list)
    =
    {| ``type`` = "doc"
       content =
        [| for todoList in todoLists do
               yield
                   JsonSerializer.Deserialize<JsonElement>(
                       $"""
                        {{
                            "type": "heading",
                            "attrs": {{
                                "textAlign": null,
                                "indent": null,
                                "lineHeight": null,
                                "level": 3
                            }},
                            "content": [
                                {{
                                    "type": "text",
                                     "marks": [
                                        {{
                                            "type": "link",
                                            "attrs": {{
                                                "href": "/view?date={todoList.noteId}",
                                                "openInNewTab": true    
                                            }}
                                        }}
                                    ],  
                                    "text": "{todoList.noteId}"
                                }}
                            ]
                        }}
                        """
                   )

               yield! todoList.todoList |] |}

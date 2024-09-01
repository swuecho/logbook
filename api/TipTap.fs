module TipTap
open System.Text.Json
open FSharp.Data


let rec getContent (jv: JsonValue) =
    let extractProperty (x, y) =
        match (x, y) with
        | ("content", y) -> getContent y
        | ("text", y) -> string y
        | (_, y) -> " "

    match jv with
    // JsonValue.Array(elements) -> Array.map getContent elements
    | JsonValue.Array(arr) -> arr |> Array.map getContent |> Util.join " "
    | JsonValue.Record(record) -> record |> Array.map extractProperty |> Util.join " "
    | JsonValue.String(str) -> str
    | JsonValue.Number(n) -> n |> string
    | JsonValue.Float(f) -> f |> string
    | JsonValue.Boolean(b) -> b |> string
    | JsonValue.Null -> ""

let getTextFromNote (note: string) =
    let content = sprintf "%s" note

    match JsonValue.TryParse content with
    | Some(json) -> getContent json
    | None -> content

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

let rec tipTapDocToMarkdown (element: JsonElement) =
    match element.GetProperty("type").GetString() with
    | "doc" ->
        element.GetProperty("content")
        |> _.EnumerateArray()
        |> Seq.map tipTapDocToMarkdown
        |> String.concat "\n"
    | "heading" ->
        let heading =
            element.GetProperty("content")
            |> _.EnumerateArray()
            |> Seq.map tipTapDocToMarkdown
            |> String.concat ""
        let level = element.GetProperty("attrs").GetProperty("level").GetInt32()
        let prefix = String.replicate level "#"
        //let marks = element.GetProperty("marks")
        // let marksStr =
        //     marks
        //     |> _.EnumerateArray()
        //     |> Seq.map (fun mark -> mark.GetProperty("type").GetString())
        //   |> String.concat " "
        "\n" + prefix + " " + heading + "\n"
    | "paragraph" ->
        element.GetProperty("content")
        |> _.EnumerateArray()
        |> Seq.map tipTapDocToMarkdown
        |> String.concat ""
    | "text" -> element.GetProperty("text").GetString()
    | "todo_list" ->
        element.GetProperty("content")
        |> _.EnumerateArray()
        |> Seq.map tipTapDocToMarkdown
        |> String.concat "\n"
    | "todo_item" ->
        let doneAttr = element.GetProperty("attrs").GetProperty("done").GetBoolean()
        let prefix = if doneAttr then "- [x] " else "- [ ] "
        let content =
            element.GetProperty("content")
            |> _.EnumerateArray()
            |> Seq.map tipTapDocToMarkdown
            |> String.concat ""
        prefix + content
    | _ -> ""

// Function to convert a TipTap doc JSON to Markdown
let tipTapDocJsonToMarkdown (json: string) =
    try
        let jsonDocument = JsonDocument.Parse(json)
        let root = jsonDocument.RootElement
        tipTapDocToMarkdown root
    with ex ->
        printfn "%A" ex
        ""

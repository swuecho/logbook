module TipTap
open System
open System.Text.Json
open FSharp.Data

let LINEBREAK = "\n"

let private isTodoListType (nodeType: string) =
    nodeType = "todo_list" || nodeType = "taskList"

let private isTodoItemType (nodeType: string) =
    nodeType = "todo_item" || nodeType = "taskItem"

type TipTapDocument =
    { ``type``: string
      content: JsonElement array }

let containsTodoNodeMarker (note: string) =
    not (String.IsNullOrEmpty note)
    && (note.Contains("todo_list", StringComparison.Ordinal)
        || note.Contains("todo_item", StringComparison.Ordinal)
        || note.Contains("taskList", StringComparison.Ordinal)
        || note.Contains("taskItem", StringComparison.Ordinal))

let private getTodoDoneAttr (element: JsonElement) =
    match element.TryGetProperty("attrs") with
    | true, attrs ->
        match attrs.TryGetProperty("done") with
        | true, doneAttr -> doneAttr.GetBoolean()
        | false, _ ->
            match attrs.TryGetProperty("checked") with
            | true, checkedAttr -> checkedAttr.GetBoolean()
            | false, _ -> false
    | false, _ -> false

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

// find all todo_list/taskList nodes in note and return them as json
let extractTodoList (note: string) =

    let rec extractTodoItems (element: JsonElement) =
        seq {
            match element.ValueKind with
            | JsonValueKind.Object ->
                match element.TryGetProperty("type") with
                | true, typeProperty when isTodoListType (typeProperty.GetString()) -> yield element
                | _ ->
                    for property in element.EnumerateObject() do
                        yield! extractTodoItems property.Value
            | JsonValueKind.Array ->
                for item in element.EnumerateArray() do
                    yield! extractTodoItems item
            | _ -> ()
        }

    if not (containsTodoNodeMarker note) then
        []
    else
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
    { ``type`` = "doc"
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

               yield! todoList.todoList |] }

(*


*)
let rec tipTapDocToMarkdown (element: JsonElement) =
    match element.GetProperty("type").GetString() with
    | "doc" ->
        match element.TryGetProperty("content") with
        | true, contentProperty ->
            contentProperty.EnumerateArray()
            |> Seq.map tipTapDocToMarkdown
            |> String.concat LINEBREAK
        | false, _ -> ""
    | "heading" ->
        match element.TryGetProperty("content") with
        | false, _ -> LINEBREAK
        | true, content ->
            let heading =
                content 
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
            LINEBREAK + prefix + " " + heading + LINEBREAK
    | "paragraph" ->
        match element.TryGetProperty("content") with
        | true, contentProperty -> 
            contentProperty.EnumerateArray()
            |> Seq.map tipTapDocToMarkdown
            |> String.concat LINEBREAK
        | false, _ -> LINEBREAK
    | "code_block" ->
        match element.TryGetProperty("content") with
        | true, contentProperty -> 
            let code_text = 
                contentProperty.EnumerateArray()
                |> Seq.map tipTapDocToMarkdown
                |> String.concat LINEBREAK
            "```" + LINEBREAK + code_text + LINEBREAK + "```"
        | false, _ -> "```" + LINEBREAK + "```"
    | "text" ->
        match element.TryGetProperty("text") with 
        | false, _ -> LINEBREAK 
        | true, text -> text.GetString()
    | nodeType when isTodoListType nodeType ->
        element.GetProperty("content")
        |> _.EnumerateArray()
        |> Seq.map tipTapDocToMarkdown
        |> String.concat LINEBREAK
    | nodeType when isTodoItemType nodeType ->
        let doneAttr = getTodoDoneAttr element
        let prefix = if doneAttr then "- [x] " else "- [ ] "
        let content =
            element.GetProperty("content")
            |> _.EnumerateArray()
            |> Seq.map tipTapDocToMarkdown
            |> String.concat ""
        prefix + content
    | _ -> 
        printfn "Unknown type: %s" (element.GetProperty("type").GetString())
        LINEBREAK 

// Function to convert a TipTap doc JSON to Markdown
let tipTapDocJsonToMarkdown (json: string) =
    try
        let jsonDocument = JsonDocument.Parse(json)
        let root = jsonDocument.RootElement
        tipTapDocToMarkdown root
    with ex ->
        printfn "%A" ex
        ""

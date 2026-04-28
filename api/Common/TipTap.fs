module TipTap
open System
open System.Text.Json
open System.Text

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

let private appendToken (sb: StringBuilder) (token: string) =
    if not (String.IsNullOrWhiteSpace token) then
        if sb.Length > 0 then sb.Append(' ') |> ignore
        sb.Append(token) |> ignore

let rec private collectText (sb: StringBuilder) (element: JsonElement) =
    match element.ValueKind with
    | JsonValueKind.Object ->
        for prop in element.EnumerateObject() do
            if prop.NameEquals("text") && prop.Value.ValueKind = JsonValueKind.String then
                appendToken sb (prop.Value.GetString())
            else
                collectText sb prop.Value
    | JsonValueKind.Array ->
        for item in element.EnumerateArray() do
            collectText sb item
    | _ -> ()

let getTextFromNote (note: string) =
    if String.IsNullOrEmpty note then
        ""
    else
        try
            use jsonDocument = JsonDocument.Parse(note)
            let sb = StringBuilder()
            collectText sb jsonDocument.RootElement
            sb.ToString()
        with _ ->
            // Not JSON (or invalid JSON); index the raw content.
            note

/// Returns true if the note has no meaningful user content.
/// This treats "empty doc skeleton" TipTap JSON (e.g. doc with an empty paragraph)
/// as empty, so it shouldn't count as a real note.
let isEffectivelyEmpty (note: string) =
    if String.IsNullOrWhiteSpace note then
        true
    else
        // If it includes todo/task nodes, keep it as non-empty even if plain text is empty.
        if containsTodoNodeMarker note then
            false
        else
            String.IsNullOrWhiteSpace(getTextFromNote note)

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
            // Do not dispose the JsonDocument here: the returned JsonElements are backed by it.
            let jsonDocument = JsonDocument.Parse(note)
            let root = jsonDocument.RootElement
            extractTodoItems root |> Seq.toList
        with _ ->
            []

let serializeTodoList (todoList: JsonElement list) =
    JsonSerializer.Serialize(todoList |> List.toArray)

let extractTodoListJson (note: string) =
    if not (containsTodoNodeMarker note) then
        None
    else
        try
            use jsonDocument = JsonDocument.Parse(note)
            let root = jsonDocument.RootElement

            let rec extractRawTodoLists (element: JsonElement) =
                seq {
                    match element.ValueKind with
                    | JsonValueKind.Object ->
                        match element.TryGetProperty("type") with
                        | true, typeProperty when isTodoListType (typeProperty.GetString()) -> yield element.GetRawText()
                        | _ ->
                            for property in element.EnumerateObject() do
                                yield! extractRawTodoLists property.Value
                    | JsonValueKind.Array ->
                        for item in element.EnumerateArray() do
                            yield! extractRawTodoLists item
                    | _ -> ()
                }

            let raw = extractRawTodoLists root |> Seq.toArray

            if raw.Length = 0 then
                None
            else
                // raw items are already JSON, so just wrap in an array.
                Some("[" + String.concat "," raw + "]")
        with _ ->
            None

let deserializeTodoList (todos: string) =
    try
        JsonSerializer.Deserialize<JsonElement array>(todos)
        |> Array.toList
    with _ ->
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
        LINEBREAK 

// Function to convert a TipTap doc JSON to Markdown
let tipTapDocJsonToMarkdown (json: string) =
    try
        let jsonDocument = JsonDocument.Parse(json)
        let root = jsonDocument.RootElement
        tipTapDocToMarkdown root
    with _ ->
        ""

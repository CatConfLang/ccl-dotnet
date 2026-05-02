namespace CclDotnet.Pacman

open System
open System.Collections.Generic
open System.Globalization
open System.Runtime.CompilerServices
open System.Text
open CatConfLang.TestRunner.Abstractions

type private Model() =
    inherit Dictionary<string, Model>(StringComparer.Ordinal)

module private Pacman =
    let private boundaryWhitespace = [| ' '; '\t'; '\n'; '\r' |]

    let private trimKey (value: string) =
        value.Trim(boundaryWhitespace)

    let private trimLeadingFirstLine (value: string) =
        let mutable index = 0
        while index < value.Length && (value[index] = ' ' || value[index] = '\t') do
            index <- index + 1
        value.Substring(index)

    let private trimTrailingWhitespace (value: string) =
        let mutable length = value.Length
        while length > 0 && (value[length - 1] = ' ' || value[length - 1] = '\t' || value[length - 1] = '\n' || value[length - 1] = '\r') do
            length <- length - 1
        value.Substring(0, length)

    let private trimValue (rawValue: string) =
        let firstNewline = rawValue.IndexOf('\n')
        let value =
            if firstNewline < 0 then
                trimLeadingFirstLine rawValue
            else
                let firstLine = rawValue.Substring(0, firstNewline) |> trimLeadingFirstLine
                firstLine + rawValue.Substring(firstNewline)

        trimTrailingWhitespace value

    let private lineEnd (text: string) (start: int) =
        let newline = text.IndexOf('\n', start)
        if newline < 0 then text.Length else newline

    let private isEmptyLine (text: string) (start: int) (finish: int) =
        let mutable index = start
        let mutable empty = true
        while empty && index < finish do
            let c = text[index]
            empty <- c = ' ' || c = '\t' || c = '\r'
            index <- index + 1
        empty

    let private countIndent (text: string) (start: int) =
        let mutable index = start
        let mutable count = 0
        while index < text.Length && (text[index] = ' ' || text[index] = '\t') do
            index <- index + 1
            count <- count + 1
        count

    let rec private skipEmptyLines (text: string) start =
        if start >= text.Length then
            text.Length
        else
            let finish = lineEnd text start
            if isEmptyLine text start finish then
                if finish < text.Length then skipEmptyLines text (finish + 1) else text.Length
            else
                start

    let indentOfFirstContentLine (text: string) =
        let start = skipEmptyLines text 0
        if start >= text.Length then 0 else countIndent text start

    let private findValueEnd (text: string) valueStart prefixLen =
        let mutable scan = valueStart
        let mutable valueEnd = text.Length
        let mutable searching = true

        while searching && scan < text.Length do
            let newline = text.IndexOf('\n', scan)
            if newline < 0 || newline + 1 >= text.Length then
                searching <- false
            else
                let nextLineStart = newline + 1
                let nextLineEnd = lineEnd text nextLineStart
                if isEmptyLine text nextLineStart nextLineEnd then
                    scan <- nextLineStart
                else
                    let indent = countIndent text nextLineStart
                    if indent <= prefixLen then
                        valueEnd <- nextLineStart
                        searching <- false
                    else
                        scan <- nextLineStart

        valueEnd

    let parseWithPrefix (prefixLen: int) (input: string) =
        let entries = ResizeArray<Entry>()
        let mutable position = skipEmptyLines input 0

        while position < input.Length do
            let delimiter = input.IndexOf('=', position)
            if delimiter < 0 then
                position <- input.Length
            else
                let key = input.Substring(position, delimiter - position) |> trimKey
                let valueStart = delimiter + 1
                let valueEnd = findValueEnd input valueStart prefixLen
                let value = input.Substring(valueStart, valueEnd - valueStart) |> trimValue
                entries.Add(Entry(key, value))
                position <- skipEmptyLines input valueEnd

        entries :> IReadOnlyList<Entry>

    let parse input =
        if String.IsNullOrEmpty input then
            Array.Empty<Entry>() :> IReadOnlyList<Entry>
        else
            parseWithPrefix 0 input

    let parseIndented input =
        if String.IsNullOrEmpty input then
            Array.Empty<Entry>() :> IReadOnlyList<Entry>
        else
            parseWithPrefix (indentOfFirstContentLine input) input

    let private mergeModel (target: Model) (source: Model) =
        let rec mergeInto (target: Model) (source: Model) =
            for KeyValue(key, value) in source do
                match target.TryGetValue(key) with
                | true, existing -> mergeInto existing value
                | false, _ -> target[key] <- value
        mergeInto target source

    let rec private modelFromValue (value: string) =
        if value.Contains("=", StringComparison.Ordinal) then
            value
            |> parseWithPrefix (indentOfFirstContentLine value)
            |> modelFromEntries
        else
            let model = Model()
            model[value] <- Model()
            model

    and modelFromEntries (entries: IReadOnlyList<Entry>) =
        let model = Model()
        for entry in entries do
            let valueModel = modelFromValue entry.Value
            match model.TryGetValue(entry.Key) with
            | true, existing -> mergeModel existing valueModel
            | false, _ -> model[entry.Key] <- valueModel
        model

    let rec private toObjectModel (model: Model) =
        let result = Dictionary<string, obj>(StringComparer.Ordinal)
        for KeyValue(key, value) in model do
            result[key] <- toObjectModel value
        result :> obj

    let buildModel input =
        input |> parse |> modelFromEntries |> toObjectModel

    let private isEmptyModel (model: Model) =
        model.Count = 0

    let private allChildrenAreLeaves (model: Model) =
        model.Count > 0 && Seq.forall isEmptyModel model.Values

    let rec private projectNode (model: Model) : obj =
        if model.Count = 1 then
            let pair = Seq.head model
            if pair.Key = "" then
                projectNode pair.Value
            elif isEmptyModel pair.Value then
                pair.Key :> obj
            elif allChildrenAreLeaves model then
                pair.Key :> obj
            else
                projectDictionary model :> obj
        elif allChildrenAreLeaves model then
            model.Keys |> Seq.map (fun key -> key :> obj) |> List<obj> :> obj
        else
            projectDictionary model :> obj

    and private projectDictionary (model: Model) =
        let result = Dictionary<string, obj>(StringComparer.Ordinal)
        for KeyValue(key, value) in model do
            result[key] <- projectNode value
        result

    let buildHierarchy input =
        input |> parse |> modelFromEntries |> projectDictionary :> obj

    let private appendEntry (builder: StringBuilder) (entry: Entry) =
        if builder.Length > 0 then
            builder.Append('\n') |> ignore

        if String.IsNullOrEmpty entry.Key then
            builder.Append("=") |> ignore
        else
            builder.Append(entry.Key).Append(" =") |> ignore
        if not (String.IsNullOrEmpty entry.Value) then
            if entry.Value.StartsWith("\n", StringComparison.Ordinal) then
                builder.Append(entry.Value) |> ignore
            else
                builder.Append(' ').Append(entry.Value) |> ignore

    let printEntries (entries: IReadOnlyList<Entry>) =
        let builder = StringBuilder()
        for entry in entries do
            appendEntry builder entry
        builder.ToString()

    let private sortedKeys (dictionary: Dictionary<string, obj>) =
        dictionary.Keys
        |> Seq.sortWith (fun left right ->
            match left = "", right = "" with
            | true, false -> -1
            | false, true -> 1
            | _ -> StringComparer.Ordinal.Compare(left, right))

    let rec private formatValue (builder: StringBuilder) (depth: int) (key: string) (value: obj) =
        let indent = String(' ', depth * 2)
        match value with
        | :? Dictionary<string, obj> as nested ->
            builder.Append(indent).Append(key).Append(" =\n") |> ignore
            formatDictionary builder (depth + 1) nested
        | :? List<obj> as items ->
            for item in items do
                match item with
                | :? Dictionary<string, obj> as nested ->
                    builder.Append(indent).Append(key).Append(" =\n") |> ignore
                    formatDictionary builder (depth + 1) nested
                | _ ->
                    builder.Append(indent).Append(key).Append(" = ").Append(item).Append('\n') |> ignore
        | _ ->
            builder.Append(indent).Append(key).Append(" = ").Append(value).Append('\n') |> ignore

    and private formatDictionary (builder: StringBuilder) depth (dictionary: Dictionary<string, obj>) =
        for key in sortedKeys dictionary do
            if key = "" then
                match dictionary[key] with
                | :? List<obj> as items ->
                    for item in items do
                        let indent = String(' ', depth * 2)
                        match item with
                        | :? Dictionary<string, obj> as nested ->
                            builder.Append(indent).Append("=\n") |> ignore
                            formatDictionary builder (depth + 1) nested
                        | _ ->
                            builder.Append(indent).Append("= ").Append(item).Append('\n') |> ignore
                | value ->
                    let indent = String(' ', depth * 2)
                    match value with
                    | :? Dictionary<string, obj> as nested ->
                        builder.Append(indent).Append("=\n") |> ignore
                        formatDictionary builder (depth + 1) nested
                    | _ ->
                        builder.Append(indent).Append("= ").Append(value).Append('\n') |> ignore
            else
                formatValue builder depth key dictionary[key]

    let canonicalFormat input =
        let builder = StringBuilder()
        match buildHierarchy input with
        | :? Dictionary<string, obj> as hierarchy -> formatDictionary builder 0 hierarchy
        | _ -> ()
        builder.ToString().TrimEnd('\n')

type CclPacmanParser() =
    interface ICclParser with
        member _.Parse(input: string) =
            Pacman.parse input

        member _.ParseIndented(input: string) =
            Pacman.parseIndented input

        member _.BuildHierarchy(input: string) =
            Pacman.buildHierarchy input

        member _.BuildModel(input: string) =
            Pacman.buildModel input

        member _.Load(input: string) =
            Pacman.buildHierarchy input

        member _.Print(input: string) =
            input |> Pacman.parse |> Pacman.printEntries

        member _.CanonicalFormat(input: string) =
            Pacman.canonicalFormat input

type CclPacmanProcessor() =
    let evaluatePredicate (entry: Entry) field op value =
        let fieldValue =
            match field with
            | "key" -> entry.Key
            | "value" -> entry.Value
            | _ -> invalidArg (nameof field) $"Unknown filter field: {field}"

        match op with
        | "==" | "eq" -> fieldValue = value
        | "!=" | "neq" -> fieldValue <> value
        | "starts_with" -> fieldValue.StartsWith(value, StringComparison.Ordinal)
        | "ends_with" -> fieldValue.EndsWith(value, StringComparison.Ordinal)
        | "contains" -> fieldValue.Contains(value, StringComparison.Ordinal)
        | _ -> invalidArg (nameof op) $"Unknown filter operator: {op}"

    interface ICclProcessing with
        member _.Filter(entries: IReadOnlyList<Entry>, field: string, op: string, value: string) =
            entries
            |> Seq.filter (fun entry -> evaluatePredicate entry field op value)
            |> ResizeArray
            :> IReadOnlyList<Entry>

        member _.Compose([<ParamArray>] inputs: string[]) =
            let parser = CclPacmanParser() :> ICclParser
            let entries = ResizeArray<Entry>()
            for input in inputs do
                if not (String.IsNullOrEmpty input) then
                    entries.AddRange(parser.Parse(input))
            entries :> IReadOnlyList<Entry>

type CclPacmanTypedAccessor() =
    let navigate (hierarchy: obj) (segments: string[]) =
        if segments.Length = 0 then
            invalidArg (nameof segments) "Path must have at least one segment."

        let mutable current = hierarchy
        for segment in segments do
            match current with
            | :? Dictionary<string, obj> as dictionary ->
                match dictionary.TryGetValue(segment) with
                | true, value -> current <- value
                | false, _ -> invalidArg (nameof segments) $"Key '{segment}' not found."
            | _ -> invalidArg (nameof segments) $"Cannot navigate through non-object at segment '{segment}'."
        current

    interface ICclTypedAccess with
        member _.GetString(hierarchy: obj, [<ParamArray>] pathSegments: string[]) =
            match navigate hierarchy pathSegments with
            | :? string as value -> value
            | _ -> invalidOp "Value at path is not a string."

        member _.GetInt(hierarchy: obj, [<ParamArray>] pathSegments: string[]) =
            match navigate hierarchy pathSegments with
            | :? string as value ->
                match Int32.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture) with
                | true, parsed -> parsed
                | false, _ -> invalidOp $"Value '{value}' is not a valid integer."
            | _ -> invalidOp "Value at path is not a string."

        member _.GetBool(hierarchy: obj, [<ParamArray>] pathSegments: string[]) =
            match navigate hierarchy pathSegments with
            | :? string as value ->
                match value.ToLowerInvariant() with
                | "true" -> true
                | "false" -> false
                | _ -> invalidOp $"Value '{value}' is not a valid boolean."
            | _ -> invalidOp "Value at path is not a string."

        member _.GetFloat(hierarchy: obj, [<ParamArray>] pathSegments: string[]) =
            match navigate hierarchy pathSegments with
            | :? string as value ->
                match Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture) with
                | true, parsed -> parsed
                | false, _ -> invalidOp $"Value '{value}' is not a valid float."
            | _ -> invalidOp "Value at path is not a string."

        member _.GetList(hierarchy: obj, [<ParamArray>] pathSegments: string[]) =
            match navigate hierarchy pathSegments with
            | :? List<obj> as values -> values :> IReadOnlyList<obj>
            | :? string as value -> List<obj>([ value :> obj ]) :> IReadOnlyList<obj>
            | _ -> invalidOp "Value at path is not a list."

type CclPacmanImplementation(?configPath: string) =
    let parser = CclPacmanParser() :> ICclParser
    let processing = CclPacmanProcessor() :> ICclProcessing
    let typedAccess = CclPacmanTypedAccessor() :> ICclTypedAccess

    interface ICclImplementation with
        member _.Name = "ccl-dotnet-pacman-fsharp"

        member _.ImplementationVersion =
            match typeof<CclPacmanParser>.Assembly.GetName().Version with
            | null -> "0.0.0"
            | version -> version.ToString()

        member _.ConfigPath = defaultArg configPath "ccl-pacman-config.yaml"

        member _.Parser = parser

        member _.Processing = processing

        member _.TypedAccess = typedAccess

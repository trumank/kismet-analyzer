namespace KismetAnalyzer;

using Newtonsoft.Json;

public class UEContext {
    [JsonProperty("classes")]
    public Dictionary<string, UEClass> Classes { get; init; }

    public static UEContext FromFile(string path) {
        using (StreamReader r = new StreamReader(path)) {
            return JsonConvert.DeserializeObject<UEContext>(r.ReadToEnd());
        }
    }
}

public class UEClass {
    [JsonProperty("functions")]
    public Dictionary<string, UEFunction> Functions { get; init; }
}

public class UEFunction {
    [JsonProperty("pure")]
    public bool Pure;
    [JsonProperty("pins")]
    public IEnumerable<UEPin> Pins { get; init; }
}

public class UEPin {
    [JsonProperty("name")]
    public string Name;
    [JsonProperty("isRef")]
    public bool IsRef;
    [JsonProperty("direction")]
    public UEPinDirection Direction;
}

public enum UEPinDirection {
    [JsonProperty("input")]
    Input,
    [JsonProperty("output")]
    Output,
}

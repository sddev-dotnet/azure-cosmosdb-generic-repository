using Newtonsoft.Json;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SDDev.Net.GenericRepository.Tests.TestModels;

public class ComplexTestObject : TestObject
{
    public List<TestAuditableObject> Complex { get; set; }
    public List<ComplexTestObject> Complexities { get; set; }
    [JsonProperty("complexString")]
    public string ComplexString { get; set; }
    [JsonPropertyName("complexity_String")]
    public string Complexity_String { get; set; }
}

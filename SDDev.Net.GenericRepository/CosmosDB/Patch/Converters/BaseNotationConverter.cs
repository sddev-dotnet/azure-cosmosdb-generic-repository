using Newtonsoft.Json;
using System.Reflection;
using System.Text.Json.Serialization;

namespace SDDev.Net.GenericRepository.CosmosDB.Patch.Converters;
public class BaseNotationConverter
{
    protected string GetJsonPropertyName(MemberInfo memberInfo)
    {
        // Newtonsoft.Json
        var jsonPropertyAttribute = memberInfo.GetCustomAttribute<JsonPropertyAttribute>();
        if (jsonPropertyAttribute != null)
        {
            return jsonPropertyAttribute.PropertyName;
        }

        // System.Text.Json
        var jsonPropertyNameAttribute = memberInfo.GetCustomAttribute<JsonPropertyNameAttribute>();
        if (jsonPropertyNameAttribute != null)
        {
            return jsonPropertyNameAttribute.Name;
        }

        // Fallback to Member Name
        return memberInfo.Name;
    }
}

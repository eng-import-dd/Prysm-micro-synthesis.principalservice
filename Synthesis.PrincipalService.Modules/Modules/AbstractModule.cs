using Nancy;
using Newtonsoft.Json;

namespace Synthesis.PrincipalService.Modules
{
    public abstract class AbstractModule : NancyModule
    {
        protected static string ToFormattedJson(object obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }
    }
}

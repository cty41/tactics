using System;
using System.IO;
using Newtonsoft.Json;

namespace Tactics.Common.Testing.Gameplay
{
    public static class ExecutableScenarioPlanLoader
    {
        public static ExecutableScenarioPlan FromJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json))
                throw new ArgumentException("Plan JSON is empty.", nameof(json));

            var plan = JsonConvert.DeserializeObject<ExecutableScenarioPlan>(json);
            if (plan == null)
                throw new InvalidOperationException("Plan JSON could not be deserialized.");

            return plan;
        }

        public static ExecutableScenarioPlan FromFile(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
                throw new ArgumentException("Plan path is empty.", nameof(path));

            return FromJson(File.ReadAllText(path));
        }
    }
}

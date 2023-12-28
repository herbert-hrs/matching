using SLTools.Api.SLSecretsApi;
using SLTools.Api.SLSecretsApi.Models;
using Serilog;
using System;

namespace Matching.Secrets
{
    public class MatchingSecrets
    {
        public RDSKeys RdsKeys { get; private set; }

        public MatchingSecrets()
        {

            SLSecretsApi SLSecretsApi = new SLSecretsApi();

            string rdsEnv = Environment.GetEnvironmentVariable("RDS_KEYS")!;
            RdsKeys = rdsEnv != null? SLSecretsApi.LoadKeys<DatabaseKeys>(rdsEnv) : null;
            if (RdsKeys == null)
            {
                Log.Warning("RdsKeys undefined. ");
            }
            
        }
    }
}
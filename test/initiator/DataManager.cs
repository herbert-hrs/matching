using System;
using Npgsql;
using SLTools.Api.SLSecretsApi.Models;

namespace MatchingTest.Initiator
{
    public class DataManager
    {
        private NpgsqlConnection  _connection;

        public DataManager(RDSKeys rdsKeys)
        {
            
            string connStatement = "Host=" + Environment.GetEnvironmentVariable("DB_HOST") +
                ";Port=" + Environment.GetEnvironmentVariable("DB_PORT") +
                ";Username=" + rdsKeys.username +
                ";Password=" + rdsKeys.password +
                ";Database=" + Environment.GetEnvironmentVariable("DB_NAME");

            _connection = new NpgsqlConnection(connStatement); 
            _connection.Open();
        }

        public void PrepareSymbols(int nSymbols)
        {
            for(int i = 1; i <= nSymbols; i++)
            {
                string symbol = $"TESTE{i}-SL";
            
                string sqlMessage = "INSERT INTO public.instrument(instrument_id, instrument_type, isin_paper, emission_date, expire_date, emission_tax, description, emitter, old_interest_rate, new_interest_rate, multiplier_percentage, index_percentage, last_update)" + 
                                    "VALUES (@symbol, '', '', '2021-12-20', '2021-12-20', '', '', '', '', '', '', '', '2021-12-20')";
                
                
                using (var cmd = new NpgsqlCommand(sqlMessage, _connection))
                {
                    cmd.Parameters.AddWithValue("symbol", symbol);
                    cmd.ExecuteNonQuery();
                }
            }

        }

         public void DeleteSymbols(int nSymbols)
        {
            for(int i = 1; i <= nSymbols; i++)
            {
                string symbol = $"TESTE{i}-SL";
            
                string sqlMessage = "DELETE FROM public.instrument where instrument_id = @symbol";
                
                using (var cmd = new NpgsqlCommand(sqlMessage, _connection))
                {
                    cmd.Parameters.AddWithValue("symbol", symbol);
                    cmd.ExecuteNonQuery();
                }
            }

        }


        public void DeleteOrders()
        {
            string sqlMessage = "DELETE FROM MATCHING.MATCH_OFFER";
            using (var cmd = new NpgsqlCommand(sqlMessage, _connection))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void Close()
        {
            _connection.Close();
        }

    }

}
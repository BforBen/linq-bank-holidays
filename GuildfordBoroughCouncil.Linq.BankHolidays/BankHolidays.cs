using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using System.Runtime.Caching;

namespace GuildfordBoroughCouncil.Linq
{
    public static partial class DateTimeExtensions
    {
        public static async Task<List<DateTime>> GetBankHolidaysAndClosures()
        {
            // https://www.gov.uk/bank-holidays
            // http://loop.guildford.gov.uk/Lists/Bank%20holidays%20and%20office%20closures/AllItems.aspx

            var MemCache = MemoryCache.Default;

            var Dates = (List<DateTime>)MemCache.Get("BankHolidaysAndClosures");

            if (Dates == null)
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = Properties.Settings.Default.LookupServiceUri;

                    var response = client.GetAsync("BankHolidaysAndClosures").Result;

                    using (HttpContent content = response.Content)
                    {
                        Dates = await content.ReadAsAsync<List<DateTime>>();
                        MemCache.Add("BankHolidaysAndClosures", Dates, new CacheItemPolicy { });
                    }
                }
            }

            return Dates;
        }

        public static DateTime? AddBusinessDays(this DateTime? source, int days, bool factorBankHolidays = true)
        {
            if (source.HasValue)
            {
                return source.Value.AddBusinessDays(days, factorBankHolidays);
            }
            else
            {
                return null;
            }
        }

        /// <summary>
        /// Adds the given number of business days to the <see cref="DateTime"/>.
        /// 
        /// Source: http://stackoverflow.com/questions/1044688/add-business-days-and-getbusinessdays
        /// </summary>
        /// <param name="current">The date to be changed.</param>
        /// <param name="days">Number of business days to be added.</param>
        /// <param name="factorBankHolidays">Factor in bank holidays as per SharePoint calendar list</param>
        /// <returns>A <see cref="DateTime"/> increased by a given number of business days.</returns>
        public static DateTime AddBusinessDays(this DateTime source, int days, bool factorBankHolidays = true)
        {
            var sign = Math.Sign(days);
            var unsignedDays = Math.Abs(days);
            var bankHolidays = new List<DateTime>();

            if (factorBankHolidays)
            {
                bankHolidays = GetBankHolidaysAndClosures().Result;
            }

            for (var i = 0; i < unsignedDays; i++)
            {
                do
                {
                    source = source.AddDays(sign);
                }
                while (source.DayOfWeek == DayOfWeek.Saturday || source.DayOfWeek == DayOfWeek.Sunday || bankHolidays.Contains(source.Date));
            }
            return source;
        }

        public static int GetBusinessDays(this DateTime start, DateTime end, bool factorBankHolidays = true)
        {
            if (start.DayOfWeek == DayOfWeek.Saturday)
            {
                start = start.AddDays(2);
            }
            else if (start.DayOfWeek == DayOfWeek.Sunday)
            {
                start = start.AddDays(1);
            }

            if (end.DayOfWeek == DayOfWeek.Saturday)
            {
                end = end.AddDays(-1);
            }
            else if (end.DayOfWeek == DayOfWeek.Sunday)
            {
                end = end.AddDays(-2);
            }

            int diff = (int)end.Subtract(start).TotalDays;

            int result = diff / 7 * 5 + diff % 7;

            if (factorBankHolidays)
            {
                // Take off the number of days
                result = result - GetBankHolidaysAndClosures().Result.Where(d => (start <= d) && (end >= d) && d.DayOfWeek != DayOfWeek.Saturday && d.DayOfWeek != DayOfWeek.Sunday).Count();
            }

            if (end.DayOfWeek < start.DayOfWeek)
            {
                return result - 2;
            }
            else
            {
                return result;
            }
        }

        public static bool IsBankHoliday(this DateTime source)
        {
            return GetBankHolidaysAndClosures().Result.Contains(source.Date);
        }
    }
}
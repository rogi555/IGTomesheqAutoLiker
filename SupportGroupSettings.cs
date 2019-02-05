using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace IGTomesheqAutoLiker
{
    public enum StartingDateMethod { LastPost, ChosenFromCalendar, LastXHours };
    public class SupportGroupSettings
    {
        public bool OnlyLike;
        public long StartingDateTimestamp;
        // 3 metody zadawania daty
        public long LastCommentedPostTimestamp;
        public long CalendarDateTimestamp;
        public long LastHoursTimestamp;
        StartingDateMethod StartingDateMethod;

        public SupportGroupSettings()
        {
            OnlyLike = false;
            StartingDateTimestamp = 0;
            LastCommentedPostTimestamp = 0;
            CalendarDateTimestamp = 0;
            LastHoursTimestamp = 0;
            StartingDateMethod = StartingDateMethod.ChosenFromCalendar;
        }

        public SupportGroupSettings(StartingDateMethod method, bool only_like, long timestamp_value)
        {
            OnlyLike = only_like;

            // wszystkie na zero, wlasciwa jest inicjowana ponizej
            StartingDateTimestamp = 0;
            LastCommentedPostTimestamp = 0;
            CalendarDateTimestamp = 0;
            LastHoursTimestamp = 0;

            switch(method)
            {
                case StartingDateMethod.LastPost:
                    LastCommentedPostTimestamp = timestamp_value;
                    StartingDateTimestamp = timestamp_value;
                    break;

                case StartingDateMethod.ChosenFromCalendar:
                    CalendarDateTimestamp = timestamp_value;
                    StartingDateTimestamp = timestamp_value;
                    break;

                case StartingDateMethod.LastXHours:
                    LastHoursTimestamp = timestamp_value;
                    StartingDateTimestamp = timestamp_value;
                    break;

                default:
                    // nothing
                    break;
            }
        }

        public bool SaveGroupSettings()
        {

            return true;
        }
    }
}

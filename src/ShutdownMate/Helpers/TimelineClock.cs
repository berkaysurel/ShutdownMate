using System;

namespace ShutdownMate.Helpers
{
    /// <summary>
    /// Çalışan zamanlayıcının zaman çizelgesi. Yaz/kış saati takibi açıkken gerçek geçen süreyi (UTC) kullanır:
    /// saat ileri ya da geri alınsa da geri sayım kaymaz; belirli saat ise saat dilimi kurallarına göre çevrilir.
    /// Kapalıyken Windows'un gösterdiği yerel saat olduğu gibi kullanılır.
    /// </summary>
    internal sealed class TimelineClock
    {
        private readonly bool _followRealTime;
        private readonly TimeZoneInfo _zone;
        private readonly Func<DateTime> _utcNow;
        private readonly TimeSpan _offset;

        public TimelineClock(bool followRealTime)
            : this(followRealTime, TimeZoneInfo.Local, () => DateTime.UtcNow)
        {
        }

        public TimelineClock(bool followRealTime, TimeZoneInfo zone, Func<DateTime> utcNow)
        {
            _followRealTime = followRealTime;
            _zone = zone;
            _utcNow = utcNow;
            _offset = zone.GetUtcOffset(DateTime.SpecifyKind(utcNow(), DateTimeKind.Utc));
        }

        /// <summary>Çizelgedeki şu an. Başlangıçta yerel saate eşittir, sonra saat değişiminden etkilenmez.</summary>
        public DateTime Now
        {
            get
            {
                if (!_followRealTime) return ToZone(_utcNow());
                return DateTime.SpecifyKind(_utcNow() + _offset, DateTimeKind.Unspecified);
            }
        }

        /// <summary>Kullanıcının seçtiği yerel saati çizelgeye çevirir.</summary>
        public DateTime FromLocal(DateTime wall)
        {
            if (!_followRealTime) return wall;

            wall = DateTime.SpecifyKind(wall, DateTimeKind.Unspecified);

            // Saat ileri alınırken atlanan dilimde (ör. 03:30) kalan saat, atlanan süre kadar ileri kayar
            if (_zone.IsInvalidTime(wall))
                wall += _zone.GetUtcOffset(wall.AddDays(1)) - _zone.GetUtcOffset(wall.AddDays(-1));

            // Saat geri alınırken iki kez yaşanan dilimde ilk geçiş esas alınır; işlem geç kalmaz
            TimeSpan offset = _zone.GetUtcOffset(wall);
            if (_zone.IsAmbiguousTime(wall))
            {
                foreach (TimeSpan candidate in _zone.GetAmbiguousTimeOffsets(wall))
                    if (candidate > offset) offset = candidate;
            }
            return wall - offset + _offset;
        }

        /// <summary>Çizelgedeki bir anı ekranda gösterilecek yerel saate çevirir.</summary>
        public DateTime ToLocal(DateTime timeline)
        {
            return _followRealTime ? ToZone(timeline - _offset) : timeline;
        }

        private DateTime ToZone(DateTime utc)
        {
            return TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), _zone);
        }
    }
}

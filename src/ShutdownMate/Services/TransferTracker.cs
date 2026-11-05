using System;
using System.Collections.Generic;
using System.Linq;

namespace ShutdownMate.Services
{
    /// <summary>
    /// Dosya aktarımının "sürüyor / sakin" kararı (saf mantık, sistemden bağımsız).
    /// Otomatik kipte eşiği aşan sürücüler (kaynak ve hedef) bir kez kilitlenir; kilitten sonra yalnızca onlar sayılır.
    /// Karar anlık değere değil son <see cref="Window"/> saniyenin ortancasına bakar: kısa ani yazmalar bitişi geciktirmez.
    /// </summary>
    internal sealed class TransferTracker
    {
        public const int Window = 20;       // karar penceresi (saniye / örnek)
        public const int LockWindow = 10;   // sürücünün "aktarım yapıyor" sayılması için gereken süre
        public const int JoinSeconds = 10;  // ilk sürücüden sonra eşi (kaynak/hedef) eklemek için tanınan süre

        private readonly Dictionary<string, List<double>> _history = new Dictionary<string, List<double>>();
        private readonly SortedSet<string> _tracked = new SortedSet<string>();
        private string _fixedDrive;         // null = otomatik
        private int _joinLeft = -1;         // -1: henüz kilitlenmedi

        public double Threshold;

        public IReadOnlyCollection<string> Tracked { get { return _tracked; } }

        /// <summary>Tek sürücü izlenir; null/boş = otomatik.</summary>
        public void SetDrive(string drive)
        {
            _fixedDrive = string.IsNullOrEmpty(drive) ? null : drive;
            _tracked.Clear();
            _joinLeft = -1;
            if (_fixedDrive != null) _tracked.Add(_fixedDrive);
        }

        /// <summary>Saniyede bir, her sürücünün o anki hızıyla (bayt/sn) çağrılır.</summary>
        public void Add(IDictionary<string, double> rates)
        {
            foreach (var kv in rates)
            {
                List<double> list;
                if (!_history.TryGetValue(kv.Key, out list)) _history[kv.Key] = list = new List<double>();
                list.Add(Math.Max(0, kv.Value));
                if (list.Count > Window) list.RemoveAt(0);
            }
            if (_fixedDrive != null) return;

            if (_joinLeft > 0) _joinLeft--;
            if (_joinLeft == 0) return; // kilit kapandı
            foreach (var kv in _history)
            {
                if (kv.Value.Count < LockWindow) continue;
                if (Median(kv.Value.Skip(kv.Value.Count - LockWindow)) < Threshold) continue;
                if (_tracked.Add(kv.Key) && _joinLeft < 0) _joinLeft = JoinSeconds;
            }
        }

        /// <summary>İzlenen sürücülerin o anki toplam hızı; izlenen sürücü yoksa -1.</summary>
        public double Instant
        {
            get
            {
                if (_tracked.Count == 0) return -1;
                double sum = 0;
                foreach (string d in _tracked)
                {
                    List<double> list;
                    if (_history.TryGetValue(d, out list) && list.Count > 0) sum += list[list.Count - 1];
                }
                return sum;
            }
        }

        /// <summary>İzlenen sürücülerin son pencere ortancası eşikte ya da üstünde mi.</summary>
        public bool Active
        {
            get
            {
                if (_tracked.Count == 0) return false;
                int n = _tracked.Max(d => _history.ContainsKey(d) ? _history[d].Count : 0);
                if (n < LockWindow) return false;
                var sums = new double[n];
                foreach (string d in _tracked)
                {
                    List<double> list;
                    if (!_history.TryGetValue(d, out list)) continue;
                    for (int i = 0; i < list.Count; i++) sums[n - list.Count + i] += list[i];
                }
                return Median(sums) >= Threshold;
            }
        }

        public static double Median(IEnumerable<double> values)
        {
            var sorted = values.OrderBy(v => v).ToList();
            return sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];
        }
    }
}

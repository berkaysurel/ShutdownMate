namespace ShutdownMate.Models
{
    /// <summary>Gözetim kaydındaki olayın türü (arayüzde simge seçimi için).</summary>
    public enum WatchEventKind
    {
        /// <summary>Zamanlayıcı başladı.</summary>
        Started,

        /// <summary>Belirli aralıklarla alınan ara görüntü.</summary>
        Periodic,

        /// <summary>İzlenen program uzun süredir boşta (iş bitti ya da takıldı).</summary>
        Idle,

        /// <summary>Boşta kalan program yeniden çalışmaya başladı.</summary>
        Resumed,

        /// <summary>İzlenen programın penceresi yanıt vermiyor.</summary>
        NotResponding,

        /// <summary>İzlenen program kapandı.</summary>
        ProgramExited,

        /// <summary>İnternet bağlantısı koptu.</summary>
        NetworkLost,

        /// <summary>İnternet bağlantısı geri geldi.</summary>
        NetworkBack,

        /// <summary>Windows hata kaydına düşen olay (program çökmesi, sürücü, disk).</summary>
        SystemError,

        /// <summary>İşlemden hemen önceki son ekran.</summary>
        Final,

        /// <summary>Zamanlayıcı bitti (işlem yapıldı, iptal edildi ya da atlandı).</summary>
        Finished,

        /// <summary>USB sürücülerin güvenle çıkarılma sonucu (sona eklendi: eski kayıtların sayıları kaymasın).</summary>
        Usb
    }
}

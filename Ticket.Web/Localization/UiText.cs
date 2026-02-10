using System.Text.RegularExpressions;
using Ticket.Infrastructure.Entities;

namespace Ticket.Web.Localization;

public static class UiText
{
    public static string TrStatus(TicketStatus s) => s switch
    {
        TicketStatus.New => "Yeni",
        TicketStatus.InProgress => "İşlemde",
        TicketStatus.WaitingCustomer => "Müşteri Bekleniyor",
        TicketStatus.Closed => "Kapalı",
        TicketStatus.Canceled => "İptal Edildi",
        _ => s.ToString()
    };

    public static string TrChannel(TicketChannel c) => c switch
    {
        TicketChannel.Manual => "Manuel",
        TicketChannel.Email => "E-posta",
        TicketChannel.Phone => "Telefon",
        _ => c.ToString()
    };

    public static string TrActivity(string type)
    {
        if (string.IsNullOrWhiteSpace(type)) return "-";

        var t = type.Trim();

        // normalize: NoteAdded / Note.Added / note_added -> noteadded / note.added
        var key = t.Replace("_", ".")
                   .Replace(" ", "")
                   .ToLowerInvariant();

        return key switch
        {
            "created" => "Ticket oluşturuldu",
            "assigned" => "Ticket atandı",

            "email.received" or "emailreceived" => "E-posta alındı",

            "note.added" or "noteadded" => "Not eklendi",

            "status.changed" or "statuschanged" => "Durum güncellendi",

            "customer.changed" or "customerchanged" => "Müşteri güncellendi",

            "problem.changed" or "problemchanged" => "Sorun/Hizmet güncellendi",

            _ => t
        };
    }

    public static string TrActivityNote(string note)
    {
        if (string.IsNullOrWhiteSpace(note)) return "";

        var s = note;

        
        s = Regex.Replace(s, @"\bStatus:\s*(\w+)\s*(→|->)\s*(\w+)\b", m =>
        {
            var from = ParseStatus(m.Groups[1].Value);
            var to = ParseStatus(m.Groups[3].Value);
            return $"Durum: {TrStatus(from)} → {TrStatus(to)}";
        }, RegexOptions.IgnoreCase);

        
        s = Regex.Replace(s, @"\bChannel:\s*(\w+)\s*(→|->)\s*(\w+)\b", m =>
        {
            var from = ParseChannel(m.Groups[1].Value);
            var to = ParseChannel(m.Groups[3].Value);
            return $"Kanal: {TrChannel(from)} → {TrChannel(to)}";
        }, RegexOptions.IgnoreCase);

        
        s = Regex.Replace(s, @"\bFrom:\s*", "Kimden: ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bTo:\s*", "Kime: ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bCc:\s*", "Bilgi: ", RegexOptions.IgnoreCase);
        s = Regex.Replace(s, @"\bSubject:\s*", "Konu: ", RegexOptions.IgnoreCase);

        return s;
    }

    public static string TrGrantMode(string? mode) => (mode ?? "").Trim().ToLowerInvariant() switch
    {
        "grant" or "granted" => "Yetki verildi",
        "deny" or "denied" => "Yetki engellendi",
        _ => mode?.Trim() ?? ""
    };



    private static TicketStatus ParseStatus(string raw)
    {
        return Enum.TryParse<TicketStatus>(raw, ignoreCase: true, out var v) ? v : default;
    }

    private static TicketChannel ParseChannel(string raw)
    {
        return Enum.TryParse<TicketChannel>(raw, ignoreCase: true, out var v) ? v : default;
    }


}

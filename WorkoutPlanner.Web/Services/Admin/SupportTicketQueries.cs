using System.Linq.Expressions;
using WorkoutPlanner.Web.Models;

namespace WorkoutPlanner.Web.Services.Admin;

/// <summary>
/// Общие для админки правила: какое обращение новое и какое ждёт ответа.
/// Ими пользуются главная, меню и список поддержки, чтобы числа везде совпадали.
/// </summary>
public static class SupportTicketQueries
{
    /// <summary>
    /// Новое: админ его ещё не открывал или пользователь написал после последнего открытия.
    /// </summary>
    public static readonly Expression<Func<SupportTicket, bool>> IsNewForAdmin =
        ticket => ticket.AdminViewedAtUtc == null ||
                  ticket.Messages.Any(message =>
                      message.SenderType == SupportMessageSenderType.User &&
                      message.CreatedAtUtc > ticket.AdminViewedAtUtc);

    /// <summary>
    /// Ждёт ответа: обращение открыто, а последнее слово за пользователем.
    /// Решённые и закрытые не ждут — их уже разобрали, даже если без ответа.
    /// </summary>
    public static readonly Expression<Func<SupportTicket, bool>> AwaitsReply =
        ticket => ticket.Status != SupportTicketStatus.Resolved &&
                  ticket.Status != SupportTicketStatus.Closed &&
                  (!ticket.Messages.Any() ||
                   ticket.Messages
                       .OrderByDescending(message => message.CreatedAtUtc)
                       .ThenByDescending(message => message.Id)
                       .Select(message => message.SenderType)
                       .FirstOrDefault() == SupportMessageSenderType.User);
}

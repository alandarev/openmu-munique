// <copyright file="TransactionOutcomeUnknownException.cs" company="MUnique">
// Licensed under the MIT License. See LICENSE file in the project root for full license information.
// </copyright>

namespace MUnique.OpenMU.Persistence;

/// <summary>
/// The exception which <see cref="IPlayerContext.RunInTransactionAsync{T}"/> throws when the commit of the transaction
/// failed in a way which doesn't tell whether the database committed it: in or after the commit call, e.g. when the
/// connection got lost before the acknowledgement arrived.
/// </summary>
/// <remarks>
/// The in-memory changes of the operation are kept as they are (no restore actions run), because restoring them could
/// contradict a committed database. The context refuses every further save and transaction, so that this in-memory
/// state is never written over the durable state; the owner of the context has to drop it and load the data again.
/// </remarks>
public class TransactionOutcomeUnknownException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TransactionOutcomeUnknownException"/> class.
    /// </summary>
    /// <param name="innerException">The exception of the commit.</param>
    public TransactionOutcomeUnknownException(Exception innerException)
        : base("The outcome of the transaction commit is unknown; the context must not be saved anymore.", innerException)
    {
    }
}

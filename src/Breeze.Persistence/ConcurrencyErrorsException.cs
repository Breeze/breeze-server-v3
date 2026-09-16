using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Breeze.Persistence {

  /// <summary>
  /// Thrown when a save fails because a row was changed or removed by someone else after it was
  /// read - an optimistic concurrency conflict.
  /// </summary>
  /// <remarks>
  /// <para>
  /// A persistence manager raises this in place of whatever its ORM throws, so that a conflict
  /// reaches the client the same way whatever is underneath: EF Core's
  /// <c>DbUpdateConcurrencyException</c> and NHibernate's <c>StaleObjectStateException</c> both
  /// arrive as 409 Conflict with <see cref="ProblemTypeUri"/> as the RFC 9457 <c>type</c>.
  /// </para>
  /// <para>
  /// 409 alone is not enough to act on, because a duplicate key is also 409 and the recovery is
  /// different - re-read and merge here, change the data there. The <c>type</c> member is what
  /// separates them, and it is stable in a way that an ORM's message text is not.
  /// </para>
  /// <para>
  /// Being an <see cref="EntityErrorsException"/>, it also carries one
  /// <see cref="EntityError"/> per conflicting row, each naming the entity type and key values.
  /// A breeze client turns those into validation errors on exactly the entities that conflicted,
  /// which is what lets an application show the user which records to look at rather than failing
  /// the whole save with one message.
  /// </para>
  /// </remarks>
  public class ConcurrencyErrorsException : EntityErrorsException {

    /// <summary> The RFC 9457 problem type for an optimistic concurrency conflict. </summary>
    public const string ProblemTypeUri = "https://breeze.github.io/problems/concurrency-conflict";

    /// <summary> The <see cref="EntityError.ErrorName"/> on each conflicting entity. </summary>
    public const string ErrorName = "ConcurrencyError";

    /// <summary> The default <see cref="EntityError.ErrorMessage"/> for a conflicting entity. </summary>
    public const string DefaultEntityMessage =
      "This record was changed or deleted by another user after it was read.";

    /// <summary> Create from the entities that conflicted. </summary>
    /// <param name="message">Explanation for the save as a whole; becomes the problem document's <c>detail</c>.</param>
    /// <param name="entityErrors">One error per conflicting entity. May be empty if the ORM does not say which rows failed.</param>
    public ConcurrencyErrorsException(string message, IEnumerable<EntityError> entityErrors)
      : base(message, entityErrors) {
      StatusCode = HttpStatusCode.Conflict;
      ProblemType = ProblemTypeUri;
    }

    /// <summary>
    /// Build the <see cref="EntityError"/> for one conflicting entity.
    /// </summary>
    /// <param name="entityTypeName">The entity type's full .NET name, as <c>Namespace.Type</c>. A breeze client normalizes it.</param>
    /// <param name="keyValues">The entity's key values, in key order, so the client can find the entity it already holds. May be null if unknown.</param>
    /// <param name="errorMessage">Message for this entity; defaults to <see cref="DefaultEntityMessage"/>.</param>
    /// <returns>An EntityError with <see cref="ErrorName"/> and no property name, so the client attaches it to the entity rather than to one of its properties.</returns>
    public static EntityError CreateEntityError(string? entityTypeName, object[]? keyValues, string? errorMessage = null) {
      return new EntityError {
        ErrorName = ErrorName,
        EntityTypeName = entityTypeName,
        KeyValues = keyValues,
        // Deliberately no PropertyName. The row is stale as a whole; blaming the concurrency
        // column would put the error on a property the user never edited and usually cannot see.
        ErrorMessage = errorMessage ?? DefaultEntityMessage,
      };
    }

    /// <summary> The message for the save as a whole, given the number of rows that conflicted. </summary>
    /// <param name="count">How many entities conflicted; 0 if the ORM did not say.</param>
    /// <returns>A message suitable for the problem document's <c>detail</c>.</returns>
    public static string CreateMessage(int count) {
      if (count <= 0) {
        return "The save failed because the data was changed or deleted by another user after it was read.";
      }
      return count == 1
        ? "The save failed because 1 record was changed or deleted by another user after it was read."
        : $"The save failed because {count} records were changed or deleted by another user after they were read.";
    }
  }
}

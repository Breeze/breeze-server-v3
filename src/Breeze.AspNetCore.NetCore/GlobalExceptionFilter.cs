using Breeze.Persistence;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;

namespace Breeze.AspNetCore {
  /// <summary> Filter to capture and return entity errors </summary>
  /// <remarks>
  /// The response is an RFC 9457 "problem details" document. By default it also carries the
  /// pre-3.0 <c>Code</c>, <c>Message</c> and <c>EntityErrors</c> members, so a breeze-client 2.x
  /// or 3.0 application reads it unchanged: RFC 9457 section 3.2 permits extension members and
  /// requires consumers to ignore ones they do not recognize, so the document is conformant with
  /// them present. See <see cref="BreezeConfig.IncludeLegacyErrorMembers"/>.
  /// </remarks>
  public class GlobalExceptionFilter : IExceptionFilter {

    /// <summary> Empty constructor </summary>
    public GlobalExceptionFilter() {}

    /// <summary>
    /// Maps an exception to an HTTP status code; return null to accept the default of 500.
    /// <para>
    /// Recognizing a duplicate-key or foreign-key violation as 409 Conflict means reading a
    /// provider-specific error number - 2627, 2601 and 547 for SQL Server, SQLSTATE 23505 and 23503
    /// for PostgreSQL - so it does not belong in this filter, which knows nothing about the
    /// database. Supply the mapping instead:
    /// </para>
    /// <code>
    /// o.Filters.Add(new GlobalExceptionFilter {
    ///   StatusCodeForException = ex => ex.GetBaseException() is SqlException { Number: 2627 or 2601 or 547 }
    ///     ? HttpStatusCode.Conflict : null
    /// });
    /// </code>
    /// </summary>
    public Func<Exception, HttpStatusCode?>? StatusCodeForException { get; set; }

    /// <summary> Process exceptions to extract EntityErrors and include them in the response </summary>
    public void OnException(ExceptionContext context) {
      var ex = context.Exception;
      var msg = ex.InnerException == null ? ex.Message : ex.Message + "--" + ex.InnerException.Message;

      var statusCode = 500;
      List<EntityError>? entityErrors = null;
      string? problemType = null;

      if (ex is EntityErrorsException eeEx) {
        statusCode = (int)eeEx.StatusCode;
        entityErrors = eeEx.EntityErrors;
        // An exception that knows what kind of problem it is names its own type; a concurrency
        // conflict does, because 409 alone does not distinguish it from a duplicate key.
        problemType = eeEx.ProblemType;
      } else {
        var mapped = StatusCodeForException?.Invoke(ex);
        if (mapped != null) statusCode = (int)mapped.Value;
      }

      var response = new ErrorDto {
        Type = problemType ?? ProblemTypeFor(statusCode, entityErrors != null),
        Title = ReasonPhrase(statusCode),
        Status = statusCode,
        Detail = msg,
      };

      if (BreezeConfig.Instance.IncludeStackTraceInErrors) {
        response.StackTrace = ex.StackTrace;
      }

      if (BreezeConfig.Instance.IncludeLegacyErrorMembers) {
        // What a pre-3.0 client reads. Code carries the real status: before 3.0 it was left at 0
        // for anything that was not an EntityErrorsException, while the HTTP status said 500.
        response.Code = statusCode;
        response.Message = msg;
        response.EntityErrors = entityErrors;
      } else if (entityErrors != null) {
        // Needed by every breeze client whatever the casing of the rest, so it moves to the RFC
        // 9457 extension member rather than disappearing. There is no standard problem-details
        // member that can say *which instance of which type* failed, which is what the client
        // needs in order to attach a ValidationError to the right entity.
        response.ProblemEntityErrors = entityErrors;
      }

      context.Result = new ObjectResult(response) {
        StatusCode = statusCode,
        DeclaredType = typeof(ErrorDto),
        ContentTypes = { "application/problem+json" }
      };
    }

    private static string ProblemTypeFor(int statusCode, bool hasEntityErrors) {
      if (hasEntityErrors) return "https://breeze.github.io/problems/entity-errors";
      // RFC 9457 section 4.2: "about:blank" says the status code is the whole story.
      return statusCode == 500 ? "https://breeze.github.io/problems/server-error" : "about:blank";
    }

    private static string ReasonPhrase(int statusCode) => statusCode switch {
      400 => "Bad Request",
      401 => "Unauthorized",
      403 => "Forbidden",
      404 => "Not Found",
      409 => "Conflict",
      422 => "Unprocessable Content",
      500 => "Internal Server Error",
      _ => "Error",
    };
  }

  /// <summary> Error object returned to the client, as an RFC 9457 problem details document. </summary>
  /// <remarks>
  /// <c>IsReference = false</c> keeps Newtonsoft's <c>$id</c> out of the document. The global
  /// settings turn on <c>PreserveReferencesHandling.Objects</c> and <c>TypeNameHandling.Objects</c>
  /// because entity payloads need <c>$id</c>/<c>$ref</c> for object graphs and <c>$type</c> for
  /// inheritance; an error document needs neither. <c>$type</c> still appears, since suppressing it
  /// for one type would take a custom converter - it is a harmless RFC 9457 extension member that a
  /// problem+json consumer ignores, but it does name the assembly.
  /// </remarks>
  [JsonObject(IsReference = false)]
  public class ErrorDto {

    // RFC 9457 members. Lowercase because the RFC names them that way, and a problem+json
    // consumer that is not a breeze client looks for exactly these.

    /// <summary> URI identifying the problem type (RFC 9457 "type") </summary>
    [JsonProperty("type")]
    public string? Type { get; set; }
    /// <summary> Short, human-readable summary of the problem type (RFC 9457 "title") </summary>
    [JsonProperty("title")]
    public string? Title { get; set; }
    /// <summary> HTTP status code (RFC 9457 "status") </summary>
    [JsonProperty("status")]
    public int Status { get; set; }
    /// <summary> Explanation specific to this occurrence (RFC 9457 "detail") </summary>
    [JsonProperty("detail")]
    public string? Detail { get; set; }

    // Extension members. RFC 9457 section 3.2 allows these and requires a consumer to ignore any
    // it does not recognize. NullValueHandling.Ignore keeps them out of the document altogether
    // when unused, rather than emitting a wall of nulls.

    /// <summary> Entity validation errors, as an RFC 9457 extension member. Used instead of
    /// <see cref="EntityErrors"/> when <see cref="BreezeConfig.IncludeLegacyErrorMembers"/> is false. </summary>
    [JsonProperty("entityErrors", NullValueHandling = NullValueHandling.Ignore)]
    public List<EntityError>? ProblemEntityErrors { get; set; }

    /// <summary> Exception stack trace. Omitted unless
    /// <see cref="BreezeConfig.IncludeStackTraceInErrors"/> is set. </summary>
    [JsonProperty("StackTrace", NullValueHandling = NullValueHandling.Ignore)]
    public string? StackTrace { get; set; }

    // Pre-3.0 members, present so existing clients need no change. Controlled by
    // BreezeConfig.IncludeLegacyErrorMembers.

    /// <summary> HTTP status code. The same value as <see cref="Status"/>; pre-3.0 clients read this. </summary>
    [JsonProperty("Code", NullValueHandling = NullValueHandling.Ignore)]
    public int? Code { get; set; }
    /// <summary> Exception message. The same value as <see cref="Detail"/>; pre-3.0 clients read this. </summary>
    [JsonProperty("Message", NullValueHandling = NullValueHandling.Ignore)]
    public string? Message { get; set; }
    /// <summary> Entity validation errors; null unless the exception was an EntityErrorsException </summary>
    [JsonProperty("EntityErrors", NullValueHandling = NullValueHandling.Ignore)]
    public List<EntityError>? EntityErrors { get; set; }

    /// <summary> Return ErrorDto as JSON </summary>
    public override string ToString() {
      return JsonConvert.SerializeObject(this);
    }
  }
}

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Breeze.Persistence {
  /// <summary>
  /// Validates entities on the server with System.ComponentModel.DataAnnotations, turning any
  /// failures into the <see cref="EntityError"/> list a Breeze client expects.
  /// </summary>
  public class DataAnnotationsValidator {

    private PersistenceManager _persistenceManager;
    /// <summary>
    /// Create a new instance.  
    /// </summary>
    /// <param name="persistenceManager">Used for getting entity keys for building EntityError objects.</param>
    public DataAnnotationsValidator(PersistenceManager persistenceManager) {
      this._persistenceManager = persistenceManager;
    }

    /// <summary> Attach a metadata (buddy) class to an entity type, so annotations declared on it are honoured. </summary>
    /// <remarks>
    /// Use this where the entity class is generated and cannot carry the attributes itself.
    /// Calling it again for the same pair does nothing, so it is safe to call before every save.
    /// </remarks>
    /// <param name="entityType">The entity type being validated.</param>
    /// <param name="metadataType">The class carrying the annotations for it.</param>
    public static void AddDescriptor(Type entityType, Type metadataType) {
      // TypeDescriptor keeps every provider it is given, and each one wraps the last. Registering
      // the pair on every save - as the test server did - grew the chain for the life of the
      // process, and validating the type walked all of it: saving a Customer took half a second
      // after a few thousand saves. So each pair is registered once.
      lock (_describedTypes) {
        if (!_describedTypes.Add((entityType, metadataType))) return;
        TypeDescriptor.AddProviderTransparent(
          new AssociatedMetadataTypeTypeDescriptionProvider(entityType, metadataType), entityType);
      }
    }

    private static readonly HashSet<(Type, Type)> _describedTypes = new HashSet<(Type, Type)>();

    /// <summary>
    /// Validate all the entities in the saveMap.
    /// </summary>
    /// <param name="saveMap">Map of type to entities.</param>
    /// <param name="throwIfInvalid">If true, throws an EntityErrorsException if any entity is invalid</param>
    /// <exception cref="EntityErrorsException">Contains all the EntityErrors.  Only thrown if throwIfInvalid is true.</exception>
    /// <returns>List containing an EntityError for each failed validation.</returns>
    public List<EntityError> ValidateEntities(Dictionary<Type, List<EntityInfo>> saveMap, bool throwIfInvalid) {
      var entityErrors = new List<EntityError>();
      foreach (var kvp in saveMap) {

        foreach (var entityInfo in kvp.Value) {
          ValidateEntity(entityInfo, entityErrors);
        }
      }
      if (throwIfInvalid && entityErrors.Any()) {
        throw new EntityErrorsException(entityErrors);
      }
      return entityErrors;
    }

    /// <summary>
    /// Validates a single entity.
    /// Skips validation (returns true) if entity is marked Deleted.
    /// </summary>
    /// <param name="entityInfo">contains the entity to validate</param>
    /// <param name="entityErrors">An EntityError is added to this list for each error found in the entity</param>
    /// <returns>true if entity is valid, false if invalid.</returns>
    public bool ValidateEntity(EntityInfo entityInfo, List<EntityError> entityErrors) {
      if (entityInfo.EntityState == EntityState.Deleted) return true;
      // Perform validation on the entity, based on DataAnnotations.  
      var entity = entityInfo.Entity;
      var validationResults = new List<ValidationResult>();
      if (!Validator.TryValidateObject(entity, new ValidationContext(entity, null, null), validationResults, true)) {
        var keyValues = _persistenceManager.GetKeyValues(entityInfo);
        var entityTypeName = entity.GetType().FullName;
        foreach (var vr in validationResults) {
          entityErrors.Add(new EntityError() {
            EntityTypeName = entityTypeName,
            ErrorMessage = vr.ErrorMessage,
            ErrorName = "ValidationError",
            KeyValues = keyValues,
            PropertyName = vr.MemberNames.FirstOrDefault()
          });
        }
        return false;
      }
      return true;
    }
  }
}

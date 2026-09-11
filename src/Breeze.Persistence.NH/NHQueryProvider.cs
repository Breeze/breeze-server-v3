using NHibernate.Engine;
using NHibernate.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Breeze.Persistence.NH {
  public class NHQueryProvider : DefaultQueryProvider {
    public NHQueryProvider(DefaultQueryProvider source) : this(source.Session, source.Collection) {
      // copy the private _options from the source
      // DefaultQueryProvider has always kept its options in a private _options field. Were a
      // future NHibernate to rename it, this copy would be wrong anyway - so fail here, loudly.
      var prop = source.GetType().GetField("_options", System.Reflection.BindingFlags.NonPublic
          | System.Reflection.BindingFlags.Instance)!;
      var options = prop.GetValue(source);
      prop.SetValue(this, options);
    }
    public NHQueryProvider(ISessionImplementor session) : this(session, null) { }
    public NHQueryProvider(ISessionImplementor session, object? collection) : base(session, collection) {
      Includes = new List<string>();
    }
    public List<string> Includes { get; }

    public new IQueryProvider WithOptions(Action<NhQueryableOptions> setOptions) {
      var qp = base.WithOptions(setOptions);
      return new NHQueryProvider((DefaultQueryProvider)qp);
    }
  }
}

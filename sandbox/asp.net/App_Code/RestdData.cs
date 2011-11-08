using System;
using System.Data.Services;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Web;

public class RestdData : DataService<RestdModel.RestdEntities>
{
  public static void InitializeService(IDataServiceConfiguration config)
  {
    config.SetEntitySetAccessRule("*", EntitySetRights.AllRead);
    config.SetServiceOperationAccessRule("*", ServiceOperationRights.All);
  }
}

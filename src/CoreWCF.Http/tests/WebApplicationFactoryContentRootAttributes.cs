// Needed to use WebApplicationFactory on Net472
using Microsoft.AspNetCore.Mvc.Testing;

[assembly: WebApplicationFactoryContentRoot(
    key: "CoreWCF.Http.Tests, Version=1.0.0.0, Culture=neutral, PublicKeyToken=null",
    contentRootPath: "",
    contentRootTest: "CoreWCF.Http.Tests.exe",
    priority: "-1000")]

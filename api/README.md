# dump

    pg_dump postgresql://hwu:using555@stock.bestqa.net:5432/hwu --table diary  > diary.sql

# restore

    psql postgresql://hwu:using555@stock.bestqa.net:5432/hwu -f diary.sql

## tag and push to aliyun

```
hwu@instance-1:~$ docker tag mcr.microsoft.com/dotnet/core/sdk:3.1-alpine registry.cn-shanghai.aliyuncs.com/bestqa/dotnet-core-sdk:3.1-alpine
hwu@instance-1:~$ docker push registry.cn-shanghai.aliyuncs.com/bestqa/dotnet-core-sdk:3.1-alpine
b7b554cc2a51: Pushed
4db87b5e5296: Pushed
742cf03e82dc: Pushed
6dd2e5075bbd: Pushed
8b11e2e6dac9: Pushed
b9dfc8eed8d6: Pushed
777b2c648970: Pushed
3.1-alpine: digest: sha256:f33d8a78fb0faee52b3eb0f29a3702f8e9b2a15bd63ce3093250bc2bf247f649 size: 1798
hwu@instance-1:~$ docker tag mcr.microsoft.com/dotnet/core/runtime:3.1-alpine registry.cn-shanghai.aliyuncs.com/bestqa/dotnet-core-runtime:3.1-alpine
hwu@instance-1:~$ docker push registry.cn-shanghai.aliyuncs.com/bestqa/dotnet-core-runtime:3.1-alpine
The push refers to repository [registry.cn-shanghai.aliyuncs.com/bestqa/dotnet-core-runtime]
8b11e2e6dac9: Mounted from bestqa/dotnet-core-sdk
b9dfc8eed8d6: Mounted from bestqa/dotnet-core-sdk
777b2c648970: Mounted from bestqa/dotnet-core-sdk
3.1-alpine: digest: sha256:e751d1e51d5ed001f5f1b7b1ae009177ed3a15c7bf7773212a5ab339c2b8f3cd size: 951
```

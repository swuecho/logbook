version=0.0.16
aliyun_registry_bestqa=registry.cn-shanghai.aliyuncs.com/bestqa
local_registry=192.168.0.100:5555
github_pkg_registry_surveyresearch=docker.pkg.github.com/swuecho/surveyresearch

tag:
	git tag release-v$(version)
	git push origin tag release-v$(version)

build:
	docker build -t $(local_registry)/notepad:$(version) .
	docker push $(local_registry)/notepad:$(version)

push:
	docker tag $(local_registry)/notepad:$(version) $(github_pkg_registry_surveyresearch)/notepad:$(version)
	docker tag $(local_registry)/notepad:$(version) $(aliyun_registry_bestqa)/notepad:$(version)
	docker push $(aliyun_registry_bestqa)/notepad:$(version)

serve:
	dotnet restore
	dotnet build
	echo $(JWT_SECRET)
	echo $(JWT_AUDIENCE)
	echo $(DATABASE_URL)
	dotnet watch run

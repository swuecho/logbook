version=0.5.7
local_registry=192.168.0.100:5555
aliyun_registry_bestqa=registry.cn-shanghai.aliyuncs.com/bestqa
github_pkg_registry_surveyresearch=docker.pkg.github.com/swuecho/surveyresearch

build:
	docker build -t echowuhao/logbook_frontend -f Dockerfile .
	docker tag  echowuhao/logbook_frontend $(local_registry)/logbook_frontend:$(version)
	docker push  $(local_registry)/logbook_frontend:$(version)
	docker tag  echowuhao/logbook_frontend $(aliyun_registry_bestqa)/logbook_frontend:$(version)
	docker push  $(aliyun_registry_bestqa)/logbook_frontend:$(version)

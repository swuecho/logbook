# build stage
FROM 192.168.0.100:5555/node:16 as build-stage

WORKDIR /app

# RUN yarn config set registry https://mirrors.huaweicloud.com/repository/npm/
# RUN yarn config set registry https://registry.npm.taobao.org
# RUN yarn config set "chromedriver_cdnurl" "https://registry.npm.taobao.org/chromedriver"

RUN npm config set registry http://registry.npm.taobao.org
# clean versions without the storybook dependency
COPY package.json ./ 
COPY yarn.lock ./ 

RUN yarn install

COPY . .


RUN yarn run build


# COPY static ./dist/static

# production stage
FROM 192.168.0.100:5555/nginx:1.15.8-alpine as production-stage

RUN mkdir -p /var/log/nginx
RUN mkdir -p /var/www/html

COPY --from=build-stage /app/dist /var/www/html

# make all files belong to the nginx user
#RUN chown -R nginx:nginx /var/www/html 
RUN chmod -R 777 /var/www/html

# Copy the respective nginx configuration files
COPY nginx_config/nginx.conf /etc/nginx/nginx.conf
COPY nginx_config/default.conf /etc/nginx/conf.d/default.conf

COPY htpasswd /etc/nginx/.htpasswd


EXPOSE 9098

CMD ["nginx", "-g", "daemon off;"]

# docker run -it -p 9098:80 --rm --name bestqa_doc echowuhao/bestqa_doc

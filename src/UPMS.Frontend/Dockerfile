FROM node:20-alpine AS build
WORKDIR /app
RUN npm install -g yarn@1.22.19

COPY vendor/eds-react-app-v17.0.0.tgz /app/vendor/eds-react-app-v17.0.0.tgz
COPY src/UPMS.Frontend/package.json /app/src/UPMS.Frontend/package.json
WORKDIR /app/src/UPMS.Frontend
RUN yarn install --non-interactive

COPY src/UPMS.Frontend /app/src/UPMS.Frontend
RUN yarn build

FROM nginx:1.27-alpine AS final
COPY src/UPMS.Frontend/nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/src/UPMS.Frontend/dist /usr/share/nginx/html
EXPOSE 80

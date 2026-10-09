# Build the Community Edition from pinned official sources.
FROM golang:1.24.6-bookworm AS server-build
WORKDIR /src
ADD https://github.com/minio/minio/archive/refs/tags/RELEASE.2025-09-07T16-13-09Z.tar.gz /tmp/source.tar.gz
RUN tar -xzf /tmp/source.tar.gz --strip-components=1 -C /src \
    && CGO_ENABLED=0 go build -trimpath -o /out/minio .

FROM golang:1.24.6-bookworm AS client-build
WORKDIR /src
ADD https://github.com/minio/mc/archive/refs/tags/RELEASE.2025-08-13T08-35-41Z.tar.gz /tmp/source.tar.gz
RUN tar -xzf /tmp/source.tar.gz --strip-components=1 -C /src \
    && CGO_ENABLED=0 go build -trimpath -o /out/mc .

FROM debian:bookworm-slim AS base
RUN apt-get update \
    && apt-get install --yes --no-install-recommends ca-certificates curl \
    && rm -rf /var/lib/apt/lists/*

FROM base AS server
COPY --from=server-build /out/minio /usr/local/bin/minio
EXPOSE 9000 9001
ENTRYPOINT ["minio"]
CMD ["server", "/data", "--console-address", ":9001"]

FROM base AS client
COPY --from=client-build /out/mc /usr/local/bin/mc
ENTRYPOINT ["mc"]

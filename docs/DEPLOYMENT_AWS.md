# Deploying to AWS

This is a reference architecture for running PhysioTrac on AWS using
services AWS offers under its Business Associate Addendum (BAA) for
covered entities/business associates. **Confirm the current list of
BAA-eligible services directly with AWS (the AWS Artifact / HIPAA
Compliance documentation) before you build on this** -- which services are
covered changes over time and is a legal question, not a technical one
this document can answer for you. Accepting the BAA in AWS Artifact is
also on you; provisioning a covered service doesn't do it automatically.
See [HIPAA_CHECKLIST.md](HIPAA_CHECKLIST.md) for the full list of what
using BAA-eligible services does *not*, by itself, get you.

## Reference architecture

| Concern | Service |
|---|---|
| `PhysioTrac.Api` / `PhysioTrac.Web` containers | Amazon ECS on Fargate (or EKS if you already run Kubernetes elsewhere) |
| Database | Amazon RDS for SQL Server |
| Secrets (connection strings, `Seed__DemoPassword`, etc.) | AWS Secrets Manager |
| TLS termination / public entry point | Application Load Balancer + AWS Certificate Manager |
| Logging | Amazon CloudWatch Logs (Serilog writes structured JSON -- see [Logging](../README.md#logging)) |
| Static frontend (`frontend/`) | Amazon S3 + CloudFront, or the same ECS cluster |
| Container images | Amazon ECR |

## Provisioning outline

1. **VPC.** Private subnets for RDS and the ECS tasks; public subnets only
   for the ALB. RDS should never be reachable from the public internet.

2. **Amazon RDS for SQL Server.** Choose an edition/tier with automated
   backups enabled and the point-in-time retention window you need (see
   [BACKUP_RESTORE.md](BACKUP_RESTORE.md)). Enable **storage encryption**
   (KMS-backed, at rest) at creation time -- this cannot be turned on
   after the fact without a snapshot-and-restore. Require **TLS** for
   connections (`TrustServerCertificate` in the connection string should
   point at a real, RDS-issued certificate chain in production, not just
   `True`-and-trust-anything as the Docker Compose dev setup does).

3. **Amazon ECR.** Push the images the existing Dockerfiles already build:

   ```bash
   aws ecr get-login-password --region <region> | docker login --username AWS --password-stdin <account-id>.dkr.ecr.<region>.amazonaws.com
   docker build -t <account-id>.dkr.ecr.<region>.amazonaws.com/physiotrac-api:latest -f src/PhysioTrac.Api/Dockerfile .
   docker push <account-id>.dkr.ecr.<region>.amazonaws.com/physiotrac-api:latest
   # repeat for src/PhysioTrac.Web/Dockerfile and frontend/Dockerfile
   ```

4. **Secrets Manager.** Store `ConnectionStrings__Default` and
   `Seed__DemoPassword` (leave this unset in real environments -- see
   [Demo data](../README.md#demo-data), it only seeds under
   `ASPNETCORE_ENVIRONMENT=Development`) as secrets. Reference them from
   the ECS task definition's `secrets` block (resolved into environment
   variables at container start by the ECS agent, from IAM permissions on
   the task role) -- never bake a secret into the task definition's plain
   `environment` block or the container image.

5. **ECS Fargate service.** One task definition per service (`api`, `web`),
   mirroring the container-to-container relationship already expressed in
   `docker-compose.yml`:
   - `ASPNETCORE_ENVIRONMENT=Production`
   - Task role with least-privilege access to only the secrets/resources
     that specific task needs
   - Service auto scaling on CPU/memory or ALB request count
   - Health checks pointed at `/health` (already implemented -- see
     [Program.cs](../src/PhysioTrac.Api/Program.cs)), matching the
     container-level `HEALTHCHECK` the Dockerfiles already define

6. **Application Load Balancer + ACM.** Public HTTPS entry point;
   redirect all HTTP to HTTPS at the listener. This is also where you'd
   attach AWS WAF.

7. **CloudWatch Logs.** Route container stdout/stderr to a log group via
   the `awslogs` driver in the task definition. As with the Azure guide,
   also route Serilog to the console sink (not only the rolling file
   sink) in production so logs are captured by the platform rather than
   lost when a Fargate task is replaced.

## What this guide does not cover

Route 53/domain setup, CI/CD wiring specifics beyond what
[.github/workflows/ci.yml](../.github/workflows/ci.yml) already builds and
tests, cost management, and multi-AZ/DR topology beyond RDS Multi-AZ --
add these deliberately for your own environment rather than copying
defaults here.

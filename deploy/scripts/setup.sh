cd /home/ubuntu/$APPLICATION_NAME/

mv /home/ubuntu/docker-compose-matching.yml /home/ubuntu/$APPLICATION_NAME/

APPLICATION_PROD="$APPLICATION_NAME-prod"

if [ $DEPLOYMENT_GROUP_NAME == $APPLICATION_PROD ]
then
    aws s3 cp s3://sl-containers-resource-prod/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/.env .
    aws s3 cp s3://sl-containers-resource-prod/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/fix.xml ./config
    aws s3 cp s3://sl-containers-resource-prod/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/Broker.cfg ./config
    aws s3 cp s3://sl-containers-resource-prod/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/Market.cfg ./config
else 
    aws s3 cp s3://sl-containers-resource/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/.env .
    aws s3 cp s3://sl-containers-resource/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/fix.xml ./config
    aws s3 cp s3://sl-containers-resource/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/Broker.cfg ./config
    aws s3 cp s3://sl-containers-resource/$APPLICATION_NAME/$DEPLOYMENT_GROUP_NAME/Market.cfg ./config
fi

. .env

AWS_REGION=${AWS_REGION//[$'\t\r\n']}
ECR_REPO=${ECR_REPO//[$'\t\r\n']}
IMAGE_NAME=${IMAGE_NAME//[$'\t\r\n']}

aws ecr get-login-password --region $AWS_REGION | sudo docker login --username AWS --password-stdin $ECR_REPO
sudo docker pull $ECR_REPO/$IMAGE_NAME:1.2.4
# Backend spec

## API side

### Create anchor

POST /api/create

body:

{
    "name": "sdfsdfs",
    "anchorId": "sdufsifjsf"
}

reply:

/content/$anchorId$

### Get anchor contents

GET /api/content/$anchorId$

reply:

{
    "message": "aldfnsdfs",
    "audio": "url to audio",
    "image": "url to image"
}

## User side

### Post content for anchor

Webpage at /content/$anchorId$

- Shows anchor name
- Fields for
  - message
  - audio upload
  - image upload
- save button commits to db
import http from 'node:http'
const server=http.createServer((request,response)=>{
 const requestPath=request.url?.startsWith('/')?request.url:'/'
 response.writeHead(308,{Location:`http://127.0.0.1:4174${requestPath}`,'Cache-Control':'no-store'})
 response.end()
})
server.listen(4173,'127.0.0.1',()=>console.log('Legacy entry redirects localhost:4173 to localhost:4174, preserving path and query.'))

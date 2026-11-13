import fs from 'node:fs/promises'
import { createHash } from 'node:crypto'
import { publicPath } from '@/server/runtime-paths'
const mime:Record<string,string>={svg:'image/svg+xml',jpg:'image/jpeg',jpeg:'image/jpeg',png:'image/png',webp:'image/webp',avif:'image/avif'}
async function serve(params:Promise<{filename:string}>,head=false){
 const {filename}=await params
 const match=/^([a-f0-9]{64})\.(svg|jpe?g|png|webp|avif)$/.exec(filename)
 if(!match)return new Response(null,{status:404,headers:{'Cache-Control':'no-store'}})
 try{const data=await fs.readFile(publicPath('media','releases',filename));if(createHash('sha256').update(data).digest('hex')!==match[1])throw new Error('Asset integrity mismatch');return new Response(head?null:new Uint8Array(data),{headers:{'Content-Type':mime[match[2]],'Content-Length':String(data.byteLength),'Cache-Control':'public, max-age=31536000, immutable','X-Content-Type-Options':'nosniff'}})}catch{return new Response(null,{status:404,headers:{'Cache-Control':'no-store'}})}
}
export const dynamic='force-dynamic'
export async function GET(_request:Request,{params}:{params:Promise<{filename:string}>}){return serve(params)}
export async function HEAD(_request:Request,{params}:{params:Promise<{filename:string}>}){return serve(params,true)}

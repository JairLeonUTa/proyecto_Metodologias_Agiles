import re,html,subprocess,unicodedata,sys,os
carrera,pagina=sys.argv[1],sys.argv[2]
subprocess.run(['curl','-s','-m','60','-o',f'{carrera}.html',f'https://uta.edu.ec/{pagina}/'])
s=open(f'{carrera}.html',encoding='utf-8').read()
os.makedirs(f'silabos-{carrera}',exist_ok=True)
items=[];vistos=set()
for m in re.finditer(r'<a[^>]+href="(https://silabo[^"]+)"[^>]*>(.*?)</a>',s,re.S):
    t=html.unescape(re.sub('<[^>]+>','',m.group(2)).strip()); cod=m.group(1).split('=')[-1]
    if cod in vistos: continue
    vistos.add(cod)
    slug=unicodedata.normalize('NFD',t).encode('ascii','ignore').decode().replace('/','-').replace(' ','_')
    items.append((cod,t,m.group(1)))
    subprocess.run(['curl','-s','-m','60','-o',f'silabos-{carrera}/{cod}_{slug}.pdf',m.group(1)])
open(f'silabos-{carrera}/indice.tsv','w').write('\n'.join('\t'.join(i) for i in items))
mallas=sorted(set(re.findall(r'href="([^"]+\.pdf)"',s)))
print(carrera,len(items),'silabos; pdfs en pagina:',[x for x in mallas if 'malla' in x.lower()][:5])

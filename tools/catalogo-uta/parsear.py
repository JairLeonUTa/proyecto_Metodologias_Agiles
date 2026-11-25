"""Extrae de cada sílabo UTA: nombre, nivel, créditos, horas totales, descripción y unidades con temas y horas."""
import json, os, re, subprocess, sys

NIVELES = {"PRIMERO":1,"SEGUNDO":2,"TERCERO":3,"CUARTO":4,"QUINTO":5,"SEXTO":6,"SEPTIMO":7,"SÉPTIMO":7,"OCTAVO":8,"NOVENO":9,"DECIMO":10,"DÉCIMO":10}

def arreglar(t):
    if "Ã" in t:
        try: return t.encode("latin-1").decode("utf-8")
        except Exception: return t
    return t

def leer(ruta):
    txt = subprocess.run(["pdftotext","-layout",ruta,"-"],capture_output=True,text=True).stdout
    lineas = [arreglar(l.rstrip()) for l in txt.splitlines()]
    plano = "\n".join(lineas)
    nombre = None
    for i,l in enumerate(lineas):
        if l.strip()=="Nombre de la asignatura":
            nombre = lineas[i+1].strip(); break
    m = re.search(r"Nivel\s*\n\s*([A-ZÉ]+)", plano)
    nivel = NIVELES.get(m.group(1), None) if m else None
    m = re.search(r"Créditos/Horas.*?\n\s*(\d+)", plano, re.S)
    creditos = int(m.group(1)) if m else None
    m = re.search(r"TOTAL DE HORAS AL SEMESTRE\s+(\d+)", plano)
    horas = int(m.group(1)) if m else None
    m = re.search(r"Descripción de la asignatura\s*(.*?)\n\s*\n", plano, re.S)
    descripcion = re.sub(r"\s+"," ",m.group(1)).strip() if m else ""
    unidades = []
    actual = None; ultimo = None
    for l in lineas:
        mu = re.match(r"^U\.(\d+)\s+(.+)$", l.strip())
        if mu:
            actual = {"numero":int(mu.group(1)),"nombre":re.sub(r"\s+"," ",mu.group(2)).strip(),"horas":0,"temas":[]}
            unidades.append(actual); ultimo=None; continue
        if actual is None: continue
        mt = re.match(r"^\s*(\d+)\.(\d+)\.?-?\s*(.+?)\s{2,}(\d+)\s+(\d+)\s+(\d+)\b", l)
        if mt:
            ultimo = {"tema":mt.group(3).strip()}; actual["temas"].append(ultimo); continue
        ms = re.match(r"^\s*SUBTOTAL HORAS\s+(\d+)\s+(\d+)\s+(\d+)", l)
        if ms:
            actual["horas"] = sum(int(x) for x in ms.groups()); ultimo=None; continue
        # continuación del nombre del tema (columna izquierda, antes de las horas)
        if ultimo is not None and re.match(r"^\s{1,8}[A-Za-zÁÉÍÓÚÑáéíóúñ(]", l):
            frag = re.split(r"\s{3,}", l.strip())[0]
            if frag and not frag.startswith(("Resultados","Aprobado","Metodolog","Estrategias","Recursos","Habilidades","Unidades")) and len(frag) < 80:
                ultimo["tema"] += " " + frag
        if l.strip().startswith(("Resultados de aprendizaje","Aprobado")): ultimo=None
    for u in unidades:
        u["contenidos"] = "; ".join(t["tema"] for t in u["temas"]); del u["temas"]
    return {"nombre":nombre,"nivel":nivel,"creditos":creditos,"horas":horas,"descripcion":descripcion,"unidades":unidades}

if __name__ == "__main__":
    carpeta, salida = sys.argv[1], sys.argv[2]
    indice = dict(l.split("\t")[:2] for l in open(os.path.join(carpeta,"indice.tsv"),encoding="utf-8").read().splitlines() if l)
    res = []
    for f in sorted(os.listdir(carpeta)):
        if not f.endswith(".pdf") or not f[:8].isdigit(): continue
        d = leer(os.path.join(carpeta,f)); d["codigo"] = f[:8]
        d["nombre"] = d["nombre"] or indice.get(f[:8])
        res.append(d)
    json.dump(res, open(salida,"w",encoding="utf-8"), ensure_ascii=False, indent=1)
    malos = [d["codigo"]+" "+(d["nombre"] or "") for d in res if not d["unidades"] or not d["horas"] or sum(u["horas"] for u in d["unidades"])==0]
    print(len(res), "asignaturas; sin unidades/horas:", malos)

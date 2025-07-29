export function exec(id, cmd, value){
    const el = document.getElementById(id);
    if(!el) return;
    el.focus();
    document.execCommand(cmd, false, value ?? null);
}

export function getHtml(id){
    const el = document.getElementById(id);
    return el ? el.innerHTML : '';
}

export function setHtml(id, html){
    const el = document.getElementById(id);
    if(el) el.innerHTML = html || '';
}

export function exec(element, cmd){
    if(!element) return;
    element.focus();
    document.execCommand(cmd);
}

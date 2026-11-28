// A join code is six digits (the web's JOIN_CODE_LENGTH); anything else never reaches the server.
const JOIN_CODE = /^\d{6}$/;

export function isJoinCode(text: string): boolean {
  return JOIN_CODE.test(text);
}

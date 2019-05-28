#define R 3
#define G 5
#define Y 6
#define B 9
#define W 10
#define T A0
#define L A1

byte msg[3];
byte len=0;

void setup() {
  Serial.begin(9600);
  pinMode(R,OUTPUT);
  pinMode(G,OUTPUT);
  pinMode(Y,OUTPUT);
  pinMode(B,OUTPUT);
  pinMode(W,OUTPUT);
  pinMode(T,INPUT);
  pinMode(L,INPUT);
}

void loop() {
  while(Serial.available()){
    byte r=Serial.read();
    if(len&&r>>7)
      len=0;
    if(len||r>>7){
      msg[len]=r;
      len++;
      if(len==3){
        runPCMsg();
        len=0;
      }
    }
  }
  sendMsgT();
  sendMsgL();
  delay(100);
}
void runPCMsg(){
  switch(msg[0]>>4){
    case 0xD:
    if(!(msg[2]&0x40)){
      byte a=msg[0]&0xF;
      byte b=msg[1]|msg[2]<<7;
      analogWrite(a,b);
    }
    break;
  }
}
void sendMsgT(){
  int m=analogRead(A0);
  Serial.write(0xE0);
  Serial.write(m&0x7F);
  Serial.write(m>>0x7);
}
void sendMsgL(){
  int m=analogRead(A1);
  Serial.write(0xE1);
  Serial.write(m&0x7F);
  Serial.write(m>>0x7);
}

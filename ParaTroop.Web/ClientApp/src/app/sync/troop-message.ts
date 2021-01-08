import { MessageType } from './message-type';

export interface TroopMessage {
  type: MessageType;
  messageId?: number;
  sourceClientId?: number;

  name?: string;
  hostname?: string;
  port?: number;
  dummy?: boolean;
  reply?: number;
  string?: string;
  constraintId?: number;
  start?: number;
  end?: number;
  operation?: Array<string | number>;
  revision?: number;
  flag?: number;
  document?: string;
  clientRanges?: number[][];
  clientLocations?: { [key: string]: number };
  index?: number;
}

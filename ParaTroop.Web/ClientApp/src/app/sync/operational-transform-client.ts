export abstract class OperationalTransformClient {
  state: IOperationalTransformState;
  revision: number = 0;

  reset(): void {
    this.revision = 0;
    this.state = Synchronized.Instance;
  }

  applyClientOperation(operation: TextOperation): void {
    this.state = this.state.applyClientOperation(this, operation);
  }

  applyServerOperation(operation: TextOperation): void {
    this.revision += 1;
    this.state = this.state.applyServerOperation(this, operation);
  }

  handleServerAck(): void {
    this.revision += 1;
    this.state = this.state.handleServerAck(this);
  }

  abstract sendOperation(operation: TextOperation): void;

  abstract applyOperation(operation: TextOperation): void;
}

export type InsertInstruction = string;
export type RetainInstruction = number;
export type DeleteInstruction = number;
export type Instruction = InsertInstruction | RetainInstruction | DeleteInstruction;

export function isRetain(instruction: Instruction): instruction is RetainInstruction {
  return typeof instruction === 'number' && instruction > 0;
}

export function isDelete(instruction: Instruction): instruction is DeleteInstruction {
  return typeof instruction === 'number' && instruction < 0;
}

export function isInsert(instruction: Instruction): instruction is InsertInstruction {
  return typeof instruction === 'string';
}

function opLength(instruction: Instruction): number {
  if (typeof instruction === 'string') {
    return instruction.length;
  } else {
    return Math.abs(instruction);
  }
}

function shortenOp(operation: Instruction, by: number): Instruction {
  if (isInsert(operation)) {
    return operation.substr(by);
  } else if (isRetain(operation)) {
    return operation - by;
  } else {
    return operation + by;
  }
}

function shortenOps(op1: Instruction, op2: Instruction): [Instruction, Instruction] {
  let len1 = opLength(op1);
  let len2 = opLength(op2);

  if (len1 == len2) {
    return [undefined,  undefined];
  } else if (len1 > len2) {
    return [shortenOp(op1, len2), undefined];
  } else {
    return [undefined,  shortenOp(op2,  len1)];
  }
}

export class TextOperation {
  instructions: (number | string)[]

  get deltaLength(): number {
    let delta = 0;

    for (const inst of this.instructions) {
      if (typeof inst === 'string') {
        delta += inst.length;
      } else if (inst < 0) {
        delta += inst;
      }
    }

    return delta;
  }

  constructor(instructions?: (number | string)[]) {
    this.instructions = instructions ? instructions.slice() : [];
  }

  retain(count: number): this {
    if (count > 0) {
      const instructionsLen = this.instructions.length;
      let lastInstruction= this.instructions[instructionsLen - 1];
      if (isRetain(lastInstruction)) {
        this.instructions[instructionsLen - 1] = lastInstruction + count;
      } else {
        this.instructions.push(count);
      }

    }

    return this;
  }

  insert(str: string): this {
    if (str) {
      const instructionsLen = this.instructions.length;
      let lastInstruction = this.instructions[instructionsLen - 1];
      if (isInsert(lastInstruction)) {
        // The last operation was also an "insert" op
        this.instructions[instructionsLen - 1] = lastInstruction + str;
      } else if (isDelete(lastInstruction)) {
        // Keep inserts before deletions
        let penultimateInstruction = this.instructions[instructionsLen - 2];
        if (isInsert(penultimateInstruction)) {
          this.instructions[instructionsLen - 2] = penultimateInstruction + str;
        } else {
          this.instructions.splice(instructionsLen - 1, 0, str);
        }
      } else {
        this.instructions.push(str);
      }
    }

    return this;
  }

  delete(count: number): this {
    if (count > 0) {
      const instructionsLen = this.instructions.length;
      let lastInstruction = this.instructions[instructionsLen - 1];
      if (isDelete(lastInstruction)) {
        this.instructions[instructionsLen - 1] = lastInstruction - count;
      } else {
        this.instructions.push(-count);
      }
    }

    return this;
  }

  apply(document: string): string {
    let pos = 0;
    let parts = [];

    for (const instruction of this.instructions) {
      if (isRetain(instruction)) {
        if (pos + instruction > document.length) {
          throw new Error('Failed to apply retain operation because it extends past the end of the document.');
        }

        parts.push(document.substr(pos, instruction));
        pos += instruction;
      } else if (isInsert(instruction)) {
        parts.push(instruction);
      } else if (isDelete(instruction)) {
        pos -= instruction;
        if (pos > document.length) {
          throw new Error('Failed to apply retain operation because it extends past the end of the document.');
        }
      } else {
        throw new Error(`Unrecognized operation type: ${instruction}`);
      }
    }

    if (pos !== document.length) {
      throw new Error('Failed to apply instructions because the total length did not match the document length');
    }

    return parts.join('');
  }

  invert(document: string): TextOperation {
    let pos = 0;
    let inverse = new TextOperation();

    for (let instruction of this.instructions) {
      if (isRetain(instruction)) {
        inverse.retain(instruction);
        pos += instruction;
      } else if (isInsert(instruction)) {
        inverse.delete(instruction.length);
      } else if (isDelete(instruction)) {
        inverse.insert(document.substr(pos, -instruction));
        pos -= instruction;
      } else {
        throw new Error(`Unrecognized operation type: ${instruction}`);
      }
    }

    return inverse;
  }

  compose(other: TextOperation): TextOperation {
    console.log(`Composing ${JSON.stringify(this.instructions)} with ${JSON.stringify(other.instructions)}`);
    let iter_a = this.instructions.values();
    let iter_b = other.instructions.values();
    let a: Instruction = undefined;
    let b: Instruction = undefined;

    let result = new TextOperation();

    while (true) {
      if (a === undefined) {
        a = iter_a.next().value;
      }
      if (b === undefined) {
        b = iter_b.next().value;
      }

      if (a === undefined && b === undefined) {
        break;
      }

      if (isDelete(a)) {
        // console.log(`Accepting delete of ${a} from this.`);
        result.delete(a);
        a = undefined;
      } else if (isInsert(b)) {
        // console.log(`Accepting insert of "${b}" from other.`);
        result.insert(b);
        b = undefined;
      } else {
        if (a === undefined) {
          throw new Error('Cannot compose instructions: first operation is too short.');
        } else if (b === undefined) {
          throw new Error('Cannot compose instructions: second operation is too short.');
        }

        const minLength = Math.min(opLength(a), opLength(b));
        if (isRetain(a) && isRetain(b)) {
          result.retain(minLength);
        } else if (isRetain(a) && isDelete(b)) {
          result.delete(minLength);
        } else if (isInsert(a) && isRetain(b)) {
          result.insert(a.substr(0, minLength));
        }

        [a, b] = shortenOps(a,  b);
      }
    }

    return result;
  }

  static transform(
    op1: TextOperation,
    op2: TextOperation): [TextOperation, TextOperation] {

    let iter_a = op1.instructions.values();
    let iter_b = op2.instructions.values();
    let a: Instruction = undefined;
    let b: Instruction = undefined;
    let a_prime = new TextOperation();
    let b_prime = new TextOperation();

    while (true) {
      if (a === undefined) {
        a = iter_a.next().value;
      }
      if (b === undefined) {
        b = iter_b.next().value;
      }

      if (a === undefined && b === undefined) {
        break;
      }

      if (isInsert(a)) {
        a_prime.insert(a);
        b_prime.retain(a.length);
        a = undefined;
      } else if (isInsert(b)) {
        a_prime.retain(b.length);
        b_prime.insert(b);
        b = undefined;
      } else {
        if (a === undefined) {
          throw new Error('Cannot transform instructions: first operation is too short.');
        } else if (b === undefined) {
          throw new Error('Cannot transform instructions: second operation is too short.');
        }

        const minLength = Math.min(opLength(a), opLength(b));
        if (isRetain(a) && isRetain(b)) {
          a_prime.retain(minLength);
          b_prime.retain(minLength);
        } else if (isRetain(a) && isDelete(b)) {
          b_prime.delete(minLength);
        } else if (isDelete(a) && isRetain(b)) {
          a_prime.delete(minLength);
        }

        [a, b] = shortenOps(a,  b);
      }
    }

    return [a_prime, b_prime];
  }
}

export interface IOperationalTransformState {
  applyClientOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState;

  applyServerOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState;

  handleServerAck(client: OperationalTransformClient): IOperationalTransformState;
}

export class Synchronized implements IOperationalTransformState {
  static Instance = new Synchronized();

  applyClientOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {

    client.sendOperation(operation);
    return new AwaitingConfirm(operation);
  }

  applyServerOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {

    client.applyOperation(operation);
    return this;
  }

  handleServerAck(client: OperationalTransformClient): IOperationalTransformState {
    throw new Error("Unable to handle server acknowledgement from the synchronized state.");
  }
}

export class AwaitingConfirm implements IOperationalTransformState {
  constructor(public outstanding: TextOperation) {
  }

  applyClientOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {

    return new AwaitingWithBuffer(this.outstanding, operation);
  }

  applyServerOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {
    let [outstanding_p, operation_p] = TextOperation.transform(this.outstanding, operation);
    client.applyOperation(operation_p);
    return new AwaitingConfirm(outstanding_p);
  }

  handleServerAck(client: OperationalTransformClient): IOperationalTransformState {
    return Synchronized.Instance;
  }
}

export class AwaitingWithBuffer implements IOperationalTransformState {
  constructor(
    public outstandingOperation,
    public bufferedOperation) {
  }

  applyClientOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {

    return new AwaitingWithBuffer(
      this.outstandingOperation,
      this.bufferedOperation.compose(operation)
    );
  }

  applyServerOperation(
    client: OperationalTransformClient,
    operation: TextOperation): IOperationalTransformState {

    let [outstanding_p, operation_p] =
      TextOperation.transform(this.outstandingOperation, operation);
    let [buffer_p, operation_pp] =
      TextOperation.transform(this.bufferedOperation, operation_p);
    client.applyOperation(operation_pp);
    return new AwaitingWithBuffer(outstanding_p, buffer_p);
  }

  handleServerAck(client: OperationalTransformClient): IOperationalTransformState {
    client.sendOperation(this.bufferedOperation);
    return new AwaitingConfirm(this.bufferedOperation);
  }

}

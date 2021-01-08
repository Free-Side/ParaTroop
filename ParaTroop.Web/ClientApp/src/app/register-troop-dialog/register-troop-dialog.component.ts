import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import * as md5 from 'blueimp-md5';

export type RegisterTroopDialogData = {
  name?: string;
  hostname?: string;
  port?: number;
}

export type RegisterTroopDialogResult = {
  name: string;
  hostname: string;
  port: number;
  passwordHash: string;
}

@Component({
  selector: 'app-register-troop-dialog',
  templateUrl: './register-troop-dialog.component.html',
  styleUrls: ['./register-troop-dialog.component.scss']
})
export class RegisterTroopDialogComponent implements OnInit {
  hidePassword: Boolean = true;
  name: string;
  hostname: string;
  port: number;
  password: string;

  constructor(
    private _dialog: MatDialogRef<RegisterTroopDialogComponent>,
    @Inject(MAT_DIALOG_DATA) data: RegisterTroopDialogData) {

    this.name = data.name;
    this.hostname = data.hostname;
    this.port = data.port || 57890;
  }

  ngOnInit(): void {
  }

  register() {
    this._dialog.close(<RegisterTroopDialogResult> {
      name: this.name,
      hostname: this.hostname,
      port: this.port,
      passwordHash: md5(this.password)
    });
  }

  cancel() {
    this._dialog.close();
  }

  registerOnEnter(event: KeyboardEvent) {
    if (event.key == 'Enter') {
      event.preventDefault();
      this.register();
    }
  }
}

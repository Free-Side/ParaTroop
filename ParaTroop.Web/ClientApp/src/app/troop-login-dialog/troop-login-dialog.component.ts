import { Component, Inject, OnInit } from '@angular/core';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import * as md5 from 'blueimp-md5';
import { Troop } from 'src/app/models/troop';

export type TroopLoginDialogData = {
  troop: Troop;
}

export type TroopLoginDialogResult = {
  username: string;
  passwordHash: string;
}

@Component({
  selector: 'app-troop-login-dialog',
  templateUrl: './troop-login-dialog.component.html',
  styleUrls: ['./troop-login-dialog.component.scss']
})
export class TroopLoginDialogComponent implements OnInit {
  public hidePassword: Boolean = true;
  public troop: Troop;
  public username: string;
  public password: string;

  constructor(
    private _dialog: MatDialogRef<TroopLoginDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: TroopLoginDialogData) {

    this.troop = data.troop;
  }

  ngOnInit(): void {
  }

  login() {
    this._dialog.close(<TroopLoginDialogResult> { username: this.username, passwordHash: md5(this.password) });
  }

  cancel() {
    this._dialog.close();
  }

  loginOnEnter(event: KeyboardEvent) {
    if (event.key == 'Enter') {
      this.login();
    }
  }
}

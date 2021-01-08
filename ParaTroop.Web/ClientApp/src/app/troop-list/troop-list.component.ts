import { Component, OnInit } from '@angular/core';
import { MatTableDataSource } from '@angular/material/table';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { MatDialog } from '@angular/material/dialog';

import { Troop } from 'src/app/models/troop';
import { environment } from 'src/environments/environment';
import {
  RegisterTroopDialogComponent,
  RegisterTroopDialogResult
} from 'src/app/register-troop-dialog/register-troop-dialog.component';

@Component({
  selector: 'app-troop-list',
  templateUrl: './troop-list.component.html',
  styleUrls: ['./troop-list.component.scss']
})
export class TroopListComponent implements OnInit {
  displayedColumns = ['name', 'join'];
  troopList: MatTableDataSource<Troop>;
  troopFilter: string;

  private _ipAddress: string;

  constructor(
    private _httpClient: HttpClient,
    private _dialog: MatDialog) {
  }

  async ngOnInit(): Promise<void> {
    this.troopList =
      new MatTableDataSource(
        await this._httpClient.get<Troop[]>(`${environment.apiRoot}/troops`).toPromise()
      );

    this._ipAddress =
      (<any> await this._httpClient.get(`${environment.apiRoot}/util/ip`).toPromise()).address;
  }

  applyFilter() {
    this.troopList.filter = this.troopFilter.trim().toLowerCase();
  }

  async registerTroop(): Promise<void> {
    const registerDialog = this._dialog.open(RegisterTroopDialogComponent, {
      width: '400px',
      data: { hostname: this._ipAddress }
    });

    const dialogResult: RegisterTroopDialogResult = await registerDialog.afterClosed().toPromise();
    if (dialogResult) {
      // TODO: display some sort of loading indicator, disable page
      try {
        let createResult = await
          this._httpClient.post<Troop>(`${environment.apiRoot}/troops`, dialogResult)
            .toPromise();

        this.troopList.data = [...this.troopList.data, createResult];
      } catch (err) {
        if (err instanceof HttpErrorResponse) {
          // TODO, handle this more gracefully: display notification, re-open dialog, maybe even
          //  highlight the problem.
          if (err.error && err.error.detail) {
            alert(err.error.detail);
          } else {
            alert(err.message);
          }
        } else {
          alert('Unknown error registering Troop.');
        }

        console.error(err);
      }
    }
  }
}
